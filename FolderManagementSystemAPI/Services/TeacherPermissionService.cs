using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class TeacherPermissionService
{
    private readonly FolderManagementDbContext _context;
    private readonly string _connectionString;

    public TeacherPermissionService(FolderManagementDbContext context)
    {
        _context = context;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    public async Task EnsurePermissionTypesAsync()
    {
        foreach (var permissionType in TeacherPermissionTypes.All)
        {
            var exists = await _context.TeacherPermissionTypes
                .AnyAsync(x => x.PermissionType == permissionType);

            if (!exists)
            {
                _context.TeacherPermissionTypes.Add(new TeacherPermissionType
                {
                    PermissionType = permissionType
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = new SqlCommand(@"
            SELECT ProgramCourseID
            FROM AcademicProgramCourses
            WHERE ProgramID = @ProgramID AND CourseID = @CourseID;
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramID", programId);
        selectCommand.Parameters.AddWithValue("@CourseID", courseId);

        var existing = await selectCommand.ExecuteScalarAsync();

        if (existing != null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO AcademicProgramCourses (ProgramID, CourseID)
            OUTPUT INSERTED.ProgramCourseID
            VALUES (@ProgramID, @CourseID);
        ", connection);

        insertCommand.Parameters.AddWithValue("@ProgramID", programId);
        insertCommand.Parameters.AddWithValue("@CourseID", courseId);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    public async Task<TeacherPermissionFlagsDto> GetTeacherFlagsAsync(
        int teacherUserId,
        int programCourseId)
    {
        await EnsurePermissionTypesAsync();
        await InitializeDefaultsIfMissingAsync(programCourseId);

        var typeMap = await _context.TeacherPermissionTypes
            .Where(x => TeacherPermissionTypes.All.Contains(x.PermissionType))
            .ToDictionaryAsync(x => x.PermissionType, x => x.PermissionTypeId);

        var permissions = await _context.TeacherPermissions
            .Where(x => x.ProgramCourseId == programCourseId && x.TeacherUserId == teacherUserId)
            .ToListAsync();

        return new TeacherPermissionFlagsDto
        {
            CanManageClos = HasAllowed(permissions, typeMap, TeacherPermissionTypes.ManageClos),
            CanUploadCourseContent = HasAllowed(permissions, typeMap, TeacherPermissionTypes.UploadCourseContent),
            CanUploadExamQuestions = HasAllowed(permissions, typeMap, TeacherPermissionTypes.UploadExamQuestions)
        };
    }

    public async Task<bool> HasPermissionAsync(
        int teacherUserId,
        int programCourseId,
        string permissionType)
    {
        var flags = await GetTeacherFlagsAsync(teacherUserId, programCourseId);
        return permissionType switch
        {
            TeacherPermissionTypes.ManageClos => flags.CanManageClos,
            TeacherPermissionTypes.UploadCourseContent => flags.CanUploadCourseContent,
            TeacherPermissionTypes.UploadExamQuestions => flags.CanUploadExamQuestions,
            _ => false
        };
    }

    public async Task<bool> HasPermissionForOfferedCourseAsync(
        int teacherUserId,
        int offeredCourseId,
        string permissionType)
    {
        var context = await GetOfferedCourseContextAsync(offeredCourseId);

        if (context == null)
        {
            return false;
        }

        return await HasPermissionAsync(teacherUserId, context.ProgramCourseId, permissionType);
    }

    public async Task InitializeDefaultsIfMissingAsync(int programCourseId)
    {
        var hasAny = await _context.TeacherPermissions
            .AnyAsync(x => x.ProgramCourseId == programCourseId);

        if (hasAny)
        {
            return;
        }

        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return;
        }

        var offerings = await _context.OfferedCourses
            .Include(x => x.Section)
            .Include(x => x.TeacherUser)
            .Where(x => x.CourseId == programCourse.CourseId)
            .Where(x => x.Section.Session.ProgramId == programCourse.ProgramId)
            .OrderBy(x => x.Section.SectionName)
            .ToListAsync();

        var defaultTeacher = offerings
            .FirstOrDefault(x => IsSectionA(x.Section.SectionName))
            ?.TeacherUserId;

        if (!defaultTeacher.HasValue)
        {
            return;
        }

        await EnsurePermissionTypesAsync();

        var typeMap = await _context.TeacherPermissionTypes
            .Where(x => TeacherPermissionTypes.All.Contains(x.PermissionType))
            .ToDictionaryAsync(x => x.PermissionType, x => x.PermissionTypeId);

        foreach (var permissionType in TeacherPermissionTypes.All)
        {
            _context.TeacherPermissions.Add(new TeacherPermission
            {
                TeacherUserId = defaultTeacher.Value,
                ProgramCourseId = programCourseId,
                PermissionTypeId = typeMap[permissionType],
                IsAllowed = true
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<HodTeacherPermissionGroupDto>> GetPermissionGroupsForCourseAsync(int courseId)
    {
        await EnsurePermissionTypesAsync();

        var offerings = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(x => x.TeacherUser)
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Section.Session.Program.ProgramName)
            .ThenBy(x => x.Section.SectionName)
            .ThenBy(x => x.TeacherUser.FullName)
            .ToListAsync();

        var groups = new List<HodTeacherPermissionGroupDto>();

        foreach (var programGroup in offerings.GroupBy(x => x.Section.Session.ProgramId))
        {
            var sample = programGroup.First();
            var programCourseId = await EnsureProgramCourseAsync(
                sample.Section.Session.ProgramId,
                sample.CourseId);

            await InitializeDefaultsIfMissingAsync(programCourseId);

            var typeMap = await _context.TeacherPermissionTypes
                .Where(x => TeacherPermissionTypes.All.Contains(x.PermissionType))
                .ToDictionaryAsync(x => x.PermissionType, x => x.PermissionTypeId);

            var permissionRows = await _context.TeacherPermissions
                .Where(x => x.ProgramCourseId == programCourseId)
                .ToListAsync();

            var teachers = programGroup
                .GroupBy(x => x.TeacherUserId)
                .Select(group =>
                {
                    var offering = group.First();
                    var teacherPermissions = permissionRows
                        .Where(x => x.TeacherUserId == group.Key)
                        .ToList();

                    return new HodTeacherPermissionRowDto
                    {
                        TeacherUserId = group.Key,
                        TeacherName = offering.TeacherUser.FullName,
                        SectionName = string.Join(", ", group.Select(x => x.Section.SectionName).Distinct()),
                        OfferedCourseId = offering.OfferedCourseId,
                        IsSectionA = group.Any(x => IsSectionA(x.Section.SectionName)),
                        CanManageClos = HasAllowed(teacherPermissions, typeMap, TeacherPermissionTypes.ManageClos),
                        CanUploadCourseContent = HasAllowed(
                            teacherPermissions,
                            typeMap,
                            TeacherPermissionTypes.UploadCourseContent),
                        CanUploadExamQuestions = HasAllowed(
                            teacherPermissions,
                            typeMap,
                            TeacherPermissionTypes.UploadExamQuestions)
                    };
                })
                .OrderByDescending(x => x.IsSectionA)
                .ThenBy(x => x.SectionName)
                .ToList();

            groups.Add(new HodTeacherPermissionGroupDto
            {
                ProgramId = sample.Section.Session.ProgramId,
                ProgramName = sample.Section.Session.Program.ProgramName,
                ProgramCourseId = programCourseId,
                CourseId = sample.CourseId,
                CourseTitle = sample.Course.CourseTitle,
                CourseCode = sample.Course.CourseCode,
                Teachers = teachers
            });
        }

        return groups;
    }

    public async Task<Dictionary<int, List<HodTeacherPermissionGroupDto>>> BuildPermissionGroupsBulkAsync(
        List<OfferedCourse> activeOfferings,
        Dictionary<(int ProgramId, int CourseId), int> programCourseIdMap)
    {
        var result = new Dictionary<int, List<HodTeacherPermissionGroupDto>>();

        if (activeOfferings.Count == 0)
        {
            return result;
        }

        var typeMap = await _context.TeacherPermissionTypes
            .AsNoTracking()
            .Where(x => TeacherPermissionTypes.All.Contains(x.PermissionType))
            .ToDictionaryAsync(x => x.PermissionType, x => x.PermissionTypeId);

        var programCourseIds = programCourseIdMap.Values.Distinct().ToList();
        var permissionRows = programCourseIds.Count == 0
            ? new List<TeacherPermission>()
            : await _context.TeacherPermissions
                .AsNoTracking()
                .Where(x => programCourseIds.Contains(x.ProgramCourseId))
                .ToListAsync();

        var permissionsByProgramCourse = permissionRows
            .GroupBy(x => x.ProgramCourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var courseGroup in activeOfferings.GroupBy(x => x.CourseId))
        {
            var groups = new List<HodTeacherPermissionGroupDto>();

            foreach (var programGroup in courseGroup.GroupBy(x => x.Section.Session.ProgramId))
            {
                var sample = programGroup.First();
                var key = (programGroup.Key, courseGroup.Key);

                if (!programCourseIdMap.TryGetValue(key, out var programCourseId))
                {
                    continue;
                }

                var rows = permissionsByProgramCourse.GetValueOrDefault(programCourseId) ?? new List<TeacherPermission>();

                var teachers = programGroup
                    .GroupBy(x => x.TeacherUserId)
                    .Select(group =>
                    {
                        var offering = group.First();
                        var teacherPermissions = rows
                            .Where(x => x.TeacherUserId == group.Key)
                            .ToList();

                        return new HodTeacherPermissionRowDto
                        {
                            TeacherUserId = group.Key,
                            TeacherName = offering.TeacherUser.FullName,
                            SectionName = string.Join(", ", group.Select(x => x.Section.SectionName).Distinct()),
                            OfferedCourseId = offering.OfferedCourseId,
                            IsSectionA = group.Any(x => IsSectionA(x.Section.SectionName)),
                            CanManageClos = HasAllowed(teacherPermissions, typeMap, TeacherPermissionTypes.ManageClos),
                            CanUploadCourseContent = HasAllowed(
                                teacherPermissions,
                                typeMap,
                                TeacherPermissionTypes.UploadCourseContent),
                            CanUploadExamQuestions = HasAllowed(
                                teacherPermissions,
                                typeMap,
                                TeacherPermissionTypes.UploadExamQuestions)
                        };
                    })
                    .OrderByDescending(x => x.IsSectionA)
                    .ThenBy(x => x.SectionName)
                    .ToList();

                groups.Add(new HodTeacherPermissionGroupDto
                {
                    ProgramId = sample.Section.Session.ProgramId,
                    ProgramName = sample.Section.Session.Program.ProgramName,
                    ProgramCourseId = programCourseId,
                    CourseId = sample.CourseId,
                    CourseTitle = sample.Course.CourseTitle,
                    CourseCode = sample.Course.CourseCode,
                    Teachers = teachers
                });
            }

            result[courseGroup.Key] = groups;
        }

        return result;
    }

    public async Task<TeacherPermissionFlagsDto> SetPermissionAsync(
        int programId,
        int courseId,
        int teacherUserId,
        string permissionType,
        bool isAllowed,
        int hodUserId)
    {
        if (!TeacherPermissionTypes.All.Contains(permissionType))
        {
            throw new InvalidOperationException("Invalid permission type.");
        }

        await EnsurePermissionTypesAsync();

        var programCourseId = await EnsureProgramCourseAsync(programId, courseId);
        await InitializeDefaultsIfMissingAsync(programCourseId);

        var typeId = await _context.TeacherPermissionTypes
            .Where(x => x.PermissionType == permissionType)
            .Select(x => x.PermissionTypeId)
            .FirstAsync();

        var teachersForCourse = await _context.OfferedCourses
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .Where(x => x.CourseId == courseId && x.Section.Session.ProgramId == programId)
            .Select(x => x.TeacherUserId)
            .Distinct()
            .ToListAsync();

        if (!teachersForCourse.Contains(teacherUserId))
        {
            throw new InvalidOperationException("Teacher is not assigned to this course in the selected program.");
        }

        if (isAllowed)
        {
            foreach (var otherTeacherId in teachersForCourse)
            {
                var row = await _context.TeacherPermissions.FirstOrDefaultAsync(x =>
                    x.ProgramCourseId == programCourseId &&
                    x.TeacherUserId == otherTeacherId &&
                    x.PermissionTypeId == typeId);

                var shouldAllow = otherTeacherId == teacherUserId;

                if (row == null)
                {
                    _context.TeacherPermissions.Add(new TeacherPermission
                    {
                        TeacherUserId = otherTeacherId,
                        ProgramCourseId = programCourseId,
                        PermissionTypeId = typeId,
                        HoduserId = hodUserId,
                        IsAllowed = shouldAllow
                    });
                }
                else
                {
                    row.IsAllowed = shouldAllow;
                    row.HoduserId = hodUserId;
                }
            }
        }
        else
        {
            var row = await _context.TeacherPermissions.FirstOrDefaultAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.TeacherUserId == teacherUserId &&
                x.PermissionTypeId == typeId);

            if (row == null)
            {
                _context.TeacherPermissions.Add(new TeacherPermission
                {
                    TeacherUserId = teacherUserId,
                    ProgramCourseId = programCourseId,
                    PermissionTypeId = typeId,
                    HoduserId = hodUserId,
                    IsAllowed = false
                });
            }
            else
            {
                row.IsAllowed = false;
                row.HoduserId = hodUserId;
            }
        }

        await _context.SaveChangesAsync();

        return await GetTeacherFlagsAsync(teacherUserId, programCourseId);
    }

    public bool CanUploadFolderTemplate(string templateCode, TeacherPermissionFlagsDto flags)
    {
        return templateCode switch
        {
            "b" => flags.CanManageClos,
            "c" => flags.CanUploadCourseContent,
            "g" => flags.CanUploadExamQuestions,
            _ => true
        };
    }

    public bool CanUploadFileCategory(string fileCategory, TeacherPermissionFlagsDto flags)
    {
        if (fileCategory is "Mid Exam Question" or "Final Exam Question")
        {
            return flags.CanUploadExamQuestions;
        }

        if (fileCategory is "CLO-GA Mapping")
        {
            return flags.CanManageClos;
        }

        if (fileCategory is "Course Content")
        {
            return flags.CanUploadCourseContent;
        }

        return true;
    }

    private async Task<OfferedCoursePermissionContext?> GetOfferedCourseContextAsync(int offeredCourseId)
    {
        var offeredCourse = await _context.OfferedCourses
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (offeredCourse == null)
        {
            return null;
        }

        var programCourseId = await EnsureProgramCourseAsync(
            offeredCourse.Section.Session.ProgramId,
            offeredCourse.CourseId);

        return new OfferedCoursePermissionContext
        {
            ProgramCourseId = programCourseId,
            ProgramId = offeredCourse.Section.Session.ProgramId,
            CourseId = offeredCourse.CourseId,
            TeacherUserId = offeredCourse.TeacherUserId
        };
    }

    private static bool HasAllowed(
        List<TeacherPermission> permissions,
        Dictionary<string, int> typeMap,
        string permissionType)
    {
        if (!typeMap.TryGetValue(permissionType, out var typeId))
        {
            return false;
        }

        return permissions.Any(x => x.PermissionTypeId == typeId && x.IsAllowed);
    }

    private static bool IsSectionA(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return false;
        }

        var name = sectionName.Trim();

        if (name.EndsWith("-A", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Length >= 2 &&
            char.ToUpperInvariant(name[^1]) == 'A' &&
            char.IsDigit(name[^2]))
        {
            return true;
        }

        if (string.Equals(name, "A", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.EndsWith(" A", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OfferedCoursePermissionContext
    {
        public int ProgramCourseId { get; set; }
        public int ProgramId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
    }
}

using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class TeacherPermissionService
{
    private static Dictionary<string, int>? _permissionTypeIdCache;
    private static readonly SemaphoreSlim TypeCacheLock = new(1, 1);
    private static int _legacyMigrationDone;

    private readonly FolderManagementDbContext _context;

    public TeacherPermissionService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task EnsurePermissionTypesAsync()
    {
        var existing = await _context.TeacherPermissionTypes
            .Select(x => x.PermissionType)
            .ToListAsync();

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var permissionType in TeacherPermissionTypes.All)
        {
            if (existingSet.Contains(permissionType))
            {
                continue;
            }

            _context.TeacherPermissionTypes.Add(new TeacherPermissionType
            {
                PermissionType = permissionType
            });
            added = true;
        }

        if (added)
        {
            await _context.SaveChangesAsync();
            _permissionTypeIdCache = null;
        }

        await MigrateLegacyPermissionsAsync();
    }

    private async Task MigrateLegacyPermissionsAsync()
    {
        if (Interlocked.CompareExchange(ref _legacyMigrationDone, 1, 0) != 0)
        {
            return;
        }

        await EnsurePermissionTypesExistOnlyAsync();

        var types = await _context.TeacherPermissionTypes.AsNoTracking().ToListAsync();
        var byName = types.ToDictionary(x => x.PermissionType, x => x.PermissionTypeId, StringComparer.OrdinalIgnoreCase);

        async Task CopyAllowedAsync(string fromName, string toName)
        {
            if (!byName.TryGetValue(fromName, out var fromId) || !byName.TryGetValue(toName, out var toId))
            {
                return;
            }

            var sourceRows = await _context.TeacherPermissions
                .Where(x => x.PermissionTypeId == fromId)
                .ToListAsync();

            var existingTargets = await _context.TeacherPermissions
                .Where(x => x.PermissionTypeId == toId)
                .Select(x => new { x.TeacherUserId, x.ProgramCourseId })
                .ToListAsync();

            var existingSet = existingTargets
                .Select(x => (x.TeacherUserId, x.ProgramCourseId))
                .ToHashSet();

            foreach (var row in sourceRows)
            {
                if (existingSet.Contains((row.TeacherUserId, row.ProgramCourseId)))
                {
                    continue;
                }

                _context.TeacherPermissions.Add(new TeacherPermission
                {
                    TeacherUserId = row.TeacherUserId,
                    ProgramCourseId = row.ProgramCourseId,
                    PermissionTypeId = toId,
                    HoduserId = row.HoduserId,
                    IsAllowed = row.IsAllowed
                });
            }
        }

        await CopyAllowedAsync(TeacherPermissionTypes.LegacyUploadCourseContent, TeacherPermissionTypes.UploadWeeklyContent);
        await CopyAllowedAsync(TeacherPermissionTypes.LegacyUploadExamQuestions, TeacherPermissionTypes.UploadMidExamQuestions);
        await CopyAllowedAsync(TeacherPermissionTypes.LegacyUploadExamQuestions, TeacherPermissionTypes.UploadFinalExamQuestions);

        // Previously CLO-PLO/Task were bundled under Manage CLOs
        if (byName.TryGetValue(TeacherPermissionTypes.ManageClos, out var closId))
        {
            var closAllowed = await _context.TeacherPermissions
                .Where(x => x.PermissionTypeId == closId && x.IsAllowed)
                .ToListAsync();

            foreach (var targetName in new[]
                     {
                         TeacherPermissionTypes.ManageCloPloMapping,
                         TeacherPermissionTypes.ManageCloTaskMapping
                     })
            {
                if (!byName.TryGetValue(targetName, out var targetId))
                {
                    continue;
                }

                var existingTargets = await _context.TeacherPermissions
                    .Where(x => x.PermissionTypeId == targetId)
                    .Select(x => new { x.TeacherUserId, x.ProgramCourseId })
                    .ToListAsync();

                var existingSet = existingTargets
                    .Select(x => (x.TeacherUserId, x.ProgramCourseId))
                    .ToHashSet();

                foreach (var row in closAllowed)
                {
                    if (existingSet.Contains((row.TeacherUserId, row.ProgramCourseId)))
                    {
                        continue;
                    }

                    _context.TeacherPermissions.Add(new TeacherPermission
                    {
                        TeacherUserId = row.TeacherUserId,
                        ProgramCourseId = row.ProgramCourseId,
                        PermissionTypeId = targetId,
                        HoduserId = row.HoduserId,
                        IsAllowed = true
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        _permissionTypeIdCache = null;
    }

    private async Task EnsurePermissionTypesExistOnlyAsync()
    {
        var existing = await _context.TeacherPermissionTypes
            .Select(x => x.PermissionType)
            .ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var permissionType in TeacherPermissionTypes.All)
        {
            if (existingSet.Contains(permissionType))
            {
                continue;
            }

            _context.TeacherPermissionTypes.Add(new TeacherPermissionType { PermissionType = permissionType });
            added = true;
        }

        if (added)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<Dictionary<string, int>> GetPermissionTypeMapAsync()
    {
        if (_permissionTypeIdCache != null &&
            _permissionTypeIdCache.Count == TeacherPermissionTypes.All.Length)
        {
            return _permissionTypeIdCache;
        }

        await TypeCacheLock.WaitAsync();
        try
        {
            if (_permissionTypeIdCache != null &&
                _permissionTypeIdCache.Count == TeacherPermissionTypes.All.Length)
            {
                return _permissionTypeIdCache;
            }

            await EnsurePermissionTypesAsync();

            var map = await _context.TeacherPermissionTypes
                .AsNoTracking()
                .Where(x => TeacherPermissionTypes.All.Contains(x.PermissionType))
                .ToDictionaryAsync(x => x.PermissionType, x => x.PermissionTypeId);

            _permissionTypeIdCache = map;
            return map;
        }
        finally
        {
            TypeCacheLock.Release();
        }
    }

    public async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
    {
        var existingId = await _context.AcademicProgramCourses
            .AsNoTracking()
            .Where(pc => pc.ProgramId == programId && pc.CourseId == courseId)
            .Select(pc => (int?)pc.ProgramCourseId)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var row = new AcademicProgramCourse
        {
            ProgramId = programId,
            CourseId = courseId
        };

        _context.AcademicProgramCourses.Add(row);
        await _context.SaveChangesAsync();
        return row.ProgramCourseId;
    }

    public async Task EnsureDefaultsForOfferedCourseAsync(int offeredCourseId)
    {
        var context = await GetOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return;
        }

        await InitializeDefaultsIfMissingAsync(context.ProgramCourseId);
    }

    /// <summary>
    /// Seeds Section A defaults using offerings already in memory (no per-course Section A DB scan).
    /// Only writes rows that are still missing. Used on create/import — not on every dashboard read.
    /// </summary>
    public async Task EnsureDefaultsFromOfferingsAsync(
        IReadOnlyList<OfferedCourse> offerings,
        IReadOnlyDictionary<(int ProgramId, int CourseId), int> programCourseIdMap)
    {
        if (offerings.Count == 0 || programCourseIdMap.Count == 0)
        {
            return;
        }

        var typeMap = await GetPermissionTypeMapAsync();
        var programCourseIds = programCourseIdMap.Values.Distinct().ToList();

        var existing = await _context.TeacherPermissions
            .AsNoTracking()
            .Where(x => programCourseIds.Contains(x.ProgramCourseId))
            .Select(x => new { x.ProgramCourseId, x.PermissionTypeId })
            .ToListAsync();

        var existingKeys = existing
            .Select(x => (x.ProgramCourseId, x.PermissionTypeId))
            .ToHashSet();

        // Fast exit when every program-course already has all permission types.
        var required = programCourseIds.Count * TeacherPermissionTypes.All.Length;
        if (existingKeys.Count >= required)
        {
            return;
        }

        var sectionAByProgramCourse = offerings
            .GroupBy(o => (o.Section.Session.ProgramId, o.CourseId))
            .ToDictionary(
                g => g.Key,
                g => g
                    .Where(o => IsSectionA(o.Section.SectionName))
                    .OrderByDescending(o => o.Section.Session.StartDate)
                    .Select(o => (int?)o.TeacherUserId)
                    .FirstOrDefault());

        var changed = false;

        foreach (var entry in programCourseIdMap)
        {
            if (!sectionAByProgramCourse.TryGetValue(entry.Key, out var sectionATeacherId) ||
                !sectionATeacherId.HasValue)
            {
                continue;
            }

            var programCourseId = entry.Value;

            foreach (var permissionType in TeacherPermissionTypes.All)
            {
                var typeId = typeMap[permissionType];
                if (existingKeys.Contains((programCourseId, typeId)))
                {
                    continue;
                }

                _context.TeacherPermissions.Add(new TeacherPermission
                {
                    TeacherUserId = sectionATeacherId.Value,
                    ProgramCourseId = programCourseId,
                    PermissionTypeId = typeId,
                    IsAllowed = true
                });
                existingKeys.Add((programCourseId, typeId));
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TeacherPermissionFlagsDto> GetTeacherFlagsAsync(
        int teacherUserId,
        int programCourseId)
    {
        var typeMap = await GetPermissionTypeMapAsync();

        var permissions = await _context.TeacherPermissions
            .AsNoTracking()
            .Where(x => x.ProgramCourseId == programCourseId && x.TeacherUserId == teacherUserId)
            .ToListAsync();

        if (permissions.Count > 0)
        {
            return BuildFlags(permissions, typeMap);
        }

        // Section A defaults: seed once only when this program-course has never been initialized.
        var anyForProgramCourse = await _context.TeacherPermissions
            .AsNoTracking()
            .AnyAsync(x => x.ProgramCourseId == programCourseId);

        if (!anyForProgramCourse)
        {
            await InitializeDefaultsIfMissingAsync(programCourseId);

            permissions = await _context.TeacherPermissions
                .AsNoTracking()
                .Where(x => x.ProgramCourseId == programCourseId && x.TeacherUserId == teacherUserId)
                .ToListAsync();
        }

        return BuildFlags(permissions, typeMap);
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
            TeacherPermissionTypes.UploadWeeklyContent => flags.CanUploadWeeklyContent,
            TeacherPermissionTypes.LegacyUploadCourseContent => flags.CanUploadWeeklyContent,
            TeacherPermissionTypes.UploadMidExamQuestions => flags.CanUploadMidExamQuestions,
            TeacherPermissionTypes.UploadFinalExamQuestions => flags.CanUploadFinalExamQuestions,
            TeacherPermissionTypes.LegacyUploadExamQuestions =>
                flags.CanUploadMidExamQuestions || flags.CanUploadFinalExamQuestions,
            TeacherPermissionTypes.ManageCloPloMapping => flags.CanManageCloPloMapping,
            TeacherPermissionTypes.ManageCloTaskMapping => flags.CanManageCloTaskMapping,
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

        var allocation = await _context.HodUploadAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (allocation != null && context.TeacherUserId == teacherUserId)
        {
            return AllocationAllows(allocation, permissionType);
        }

        return await HasPermissionAsync(teacherUserId, context.ProgramCourseId, permissionType);
    }

    public static bool AllocationAllows(HodUploadAllocation allocation, string permissionType)
    {
        return permissionType switch
        {
            TeacherPermissionTypes.ManageClos => allocation.Clos || allocation.FolderTemplate,
            TeacherPermissionTypes.UploadWeeklyContent => allocation.FolderTemplate || allocation.Clos,
            TeacherPermissionTypes.LegacyUploadCourseContent => allocation.FolderTemplate || allocation.Clos,
            TeacherPermissionTypes.UploadMidExamQuestions => allocation.Mid,
            TeacherPermissionTypes.UploadFinalExamQuestions => allocation.Final,
            TeacherPermissionTypes.LegacyUploadExamQuestions => allocation.Mid || allocation.Final,
            TeacherPermissionTypes.ManageCloPloMapping => allocation.CloPloMapping,
            TeacherPermissionTypes.ManageCloTaskMapping => allocation.CloTaskMapping,
            _ => false
        };
    }

    public async Task InitializeDefaultsIfMissingAsync(int programCourseId)
    {
        var typeMap = await GetPermissionTypeMapAsync();

        // Fast path: skip Section A lookup when every permission type already has a row.
        var typeIds = TeacherPermissionTypes.All
            .Select(name => typeMap[name])
            .ToList();

        var existingTypeIds = await _context.TeacherPermissions
            .AsNoTracking()
            .Where(x => x.ProgramCourseId == programCourseId && typeIds.Contains(x.PermissionTypeId))
            .Select(x => x.PermissionTypeId)
            .Distinct()
            .ToListAsync();

        if (existingTypeIds.Count >= typeIds.Count)
        {
            return;
        }

        var sectionATeacherId = await FindSectionATeacherUserIdAsync(programCourseId);

        if (!sectionATeacherId.HasValue)
        {
            return;
        }

        var existing = await _context.TeacherPermissions
            .Where(x => x.ProgramCourseId == programCourseId)
            .ToListAsync();

        var changed = false;

        foreach (var permissionType in TeacherPermissionTypes.All)
        {
            var typeId = typeMap[permissionType];
            var anyoneAllowed = existing.Any(x => x.PermissionTypeId == typeId && x.IsAllowed);
            if (anyoneAllowed)
            {
                continue;
            }

            var anyRowForType = existing.Any(x => x.PermissionTypeId == typeId);
            if (anyRowForType)
            {
                // Someone has an explicit denied/unassigned row; do not override HOD choices.
                continue;
            }

            var row = new TeacherPermission
            {
                TeacherUserId = sectionATeacherId.Value,
                ProgramCourseId = programCourseId,
                PermissionTypeId = typeId,
                IsAllowed = true
            };

            _context.TeacherPermissions.Add(row);
            existing.Add(row);
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<int?> FindSectionATeacherUserIdAsync(int programCourseId)
    {
        var programCourse = await _context.AcademicProgramCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return null;
        }

        // Lightweight projection — avoid tracking + full graph materialization.
        var candidates = await _context.OfferedCourses
            .AsNoTracking()
            .Where(x => x.CourseId == programCourse.CourseId)
            .Where(x => x.Section.Session.ProgramId == programCourse.ProgramId)
            .OrderByDescending(x => x.Section.Session.StartDate)
            .Select(x => new
            {
                x.TeacherUserId,
                x.Section.SectionName
            })
            .ToListAsync();

        return candidates
            .FirstOrDefault(x => IsSectionA(x.SectionName))
            ?.TeacherUserId;
    }

    public async Task<List<HodTeacherPermissionGroupDto>> GetPermissionGroupsForCourseAsync(int courseId)
    {
        var typeMap = await GetPermissionTypeMapAsync();

        var offerings = await _context.OfferedCourses
            .AsNoTracking()
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

        if (offerings.Count == 0)
        {
            return new List<HodTeacherPermissionGroupDto>();
        }

        var programCourseIdMap = await EnsureProgramCoursesBulkAsync(
            offerings
                .Select(o => (o.Section.Session.ProgramId, o.CourseId))
                .Distinct()
                .ToList());

        await EnsureDefaultsFromOfferingsAsync(offerings, programCourseIdMap);

        var programCourseIds = programCourseIdMap.Values.Distinct().ToList();
        var permissionRows = await _context.TeacherPermissions
            .AsNoTracking()
            .Where(x => programCourseIds.Contains(x.ProgramCourseId))
            .ToListAsync();

        var permissionsByProgramCourse = permissionRows
            .GroupBy(x => x.ProgramCourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var groups = new List<HodTeacherPermissionGroupDto>();

        foreach (var programGroup in offerings.GroupBy(x => x.Section.Session.ProgramId))
        {
            var sample = programGroup.First();
            if (!programCourseIdMap.TryGetValue((programGroup.Key, courseId), out var programCourseId))
            {
                continue;
            }

            var rows = permissionsByProgramCourse.GetValueOrDefault(programCourseId)
                ?? new List<TeacherPermission>();
            var typesWithRows = rows.Select(r => r.PermissionTypeId).ToHashSet();

            var teachers = programGroup
                .GroupBy(x => x.TeacherUserId)
                .Select(group => BuildTeacherRow(group, rows, typeMap, typesWithRows))
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

        var typeMap = await GetPermissionTypeMapAsync();

        // Dashboard hot path: read only. Section A defaults are applied in-memory when a
        // permission type has never been assigned yet (still shows as granted for Section A).
        // Persisted seeding happens on offered-course create/import (and lazy once if empty).
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
                var typesWithRows = rows.Select(r => r.PermissionTypeId).ToHashSet();

                var teachers = programGroup
                    .GroupBy(x => x.TeacherUserId)
                    .Select(group => BuildTeacherRow(group, rows, typeMap, typesWithRows))
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

    private async Task<Dictionary<(int ProgramId, int CourseId), int>> EnsureProgramCoursesBulkAsync(
        List<(int ProgramId, int CourseId)> keys)
    {
        var result = new Dictionary<(int ProgramId, int CourseId), int>();
        if (keys.Count == 0)
        {
            return result;
        }

        var programIds = keys.Select(k => k.ProgramId).Distinct().ToList();
        var courseIds = keys.Select(k => k.CourseId).Distinct().ToList();

        var existing = await _context.AcademicProgramCourses
            .Where(pc => programIds.Contains(pc.ProgramId) && courseIds.Contains(pc.CourseId))
            .ToListAsync();

        foreach (var pc in existing)
        {
            result[(pc.ProgramId, pc.CourseId)] = pc.ProgramCourseId;
        }

        var missing = keys.Where(k => !result.ContainsKey(k)).Distinct().ToList();
        if (missing.Count == 0)
        {
            return result;
        }

        var created = missing
            .Select(key => new AcademicProgramCourse
            {
                ProgramId = key.ProgramId,
                CourseId = key.CourseId
            })
            .ToList();

        _context.AcademicProgramCourses.AddRange(created);
        await _context.SaveChangesAsync();

        foreach (var pc in created)
        {
            result[(pc.ProgramId, pc.CourseId)] = pc.ProgramCourseId;
        }

        return result;
    }

    private static HodTeacherPermissionRowDto BuildTeacherRow(
        IGrouping<int, OfferedCourse> group,
        List<TeacherPermission> rows,
        Dictionary<string, int> typeMap,
        HashSet<int>? typesWithAnyRow = null)
    {
        var offering = group.First();
        var teacherPermissions = rows
            .Where(x => x.TeacherUserId == group.Key)
            .ToList();
        var flags = BuildFlags(teacherPermissions, typeMap);
        var isSectionA = group.Any(x => IsSectionA(x.Section.SectionName));

        // Necessary Section A default: if a permission type was never assigned for this
        // program-course, treat Section A as allowed (no DB write on dashboard load).
        if (isSectionA && typesWithAnyRow != null)
        {
            ApplyVirtualSectionADefaults(flags, typeMap, typesWithAnyRow);
        }

        return new HodTeacherPermissionRowDto
        {
            TeacherUserId = group.Key,
            TeacherName = offering.TeacherUser.FullName,
            SectionName = string.Join(", ", group.Select(x => x.Section.SectionName).Distinct()),
            OfferedCourseId = offering.OfferedCourseId,
            IsSectionA = isSectionA,
            CanManageClos = flags.CanManageClos,
            CanUploadWeeklyContent = flags.CanUploadWeeklyContent,
            CanUploadMidExamQuestions = flags.CanUploadMidExamQuestions,
            CanUploadFinalExamQuestions = flags.CanUploadFinalExamQuestions,
            CanManageCloPloMapping = flags.CanManageCloPloMapping,
            CanManageCloTaskMapping = flags.CanManageCloTaskMapping
        };
    }

    private static void ApplyVirtualSectionADefaults(
        TeacherPermissionFlagsDto flags,
        Dictionary<string, int> typeMap,
        HashSet<int> typesWithAnyRow)
    {
        bool Missing(string permissionType) =>
            !typeMap.TryGetValue(permissionType, out var typeId) || !typesWithAnyRow.Contains(typeId);

        if (Missing(TeacherPermissionTypes.ManageClos))
        {
            flags.CanManageClos = true;
        }

        if (Missing(TeacherPermissionTypes.UploadWeeklyContent) &&
            Missing(TeacherPermissionTypes.LegacyUploadCourseContent))
        {
            flags.CanUploadWeeklyContent = true;
        }

        if (Missing(TeacherPermissionTypes.UploadMidExamQuestions) &&
            Missing(TeacherPermissionTypes.LegacyUploadExamQuestions))
        {
            flags.CanUploadMidExamQuestions = true;
        }

        if (Missing(TeacherPermissionTypes.UploadFinalExamQuestions) &&
            Missing(TeacherPermissionTypes.LegacyUploadExamQuestions))
        {
            flags.CanUploadFinalExamQuestions = true;
        }

        if (Missing(TeacherPermissionTypes.ManageCloPloMapping))
        {
            flags.CanManageCloPloMapping = true;
        }

        if (Missing(TeacherPermissionTypes.ManageCloTaskMapping))
        {
            flags.CanManageCloTaskMapping = true;
        }
    }

    public async Task<UpdateTeacherPermissionResultDto> SetPermissionAsync(
        int programId,
        int courseId,
        int teacherUserId,
        string permissionType,
        bool isAllowed,
        int hodUserId,
        bool exclusive = true)
    {
        // Accept legacy names from older clients
        permissionType = permissionType switch
        {
            TeacherPermissionTypes.LegacyUploadCourseContent => TeacherPermissionTypes.UploadWeeklyContent,
            TeacherPermissionTypes.LegacyUploadExamQuestions => TeacherPermissionTypes.UploadMidExamQuestions,
            _ => permissionType
        };

        if (!TeacherPermissionTypes.All.Contains(permissionType))
        {
            throw new InvalidOperationException("Invalid permission type.");
        }

        var typeMap = await GetPermissionTypeMapAsync();
        if (!typeMap.TryGetValue(permissionType, out var typeId))
        {
            throw new InvalidOperationException("Invalid permission type.");
        }

        // Sequential queries — EF Core DbContext is not safe for concurrent operations.
        var programCourseId = await _context.AcademicProgramCourses
            .AsNoTracking()
            .Where(pc => pc.ProgramId == programId && pc.CourseId == courseId)
            .Select(pc => (int?)pc.ProgramCourseId)
            .FirstOrDefaultAsync();

        var teacherAssigned = await _context.OfferedCourses
            .AsNoTracking()
            .AnyAsync(x =>
                x.CourseId == courseId &&
                x.Section.Session.ProgramId == programId &&
                x.TeacherUserId == teacherUserId);

        if (!teacherAssigned)
        {
            throw new InvalidOperationException("Teacher is not assigned to this course in the selected program.");
        }

        if (!programCourseId.HasValue)
        {
            programCourseId = await EnsureProgramCourseAsync(programId, courseId);
        }

        // Load only rows for this permission type on this program-course.
        var existingRows = await _context.TeacherPermissions
            .Where(x =>
                x.ProgramCourseId == programCourseId.Value &&
                x.PermissionTypeId == typeId)
            .ToListAsync();

        if (isAllowed)
        {
            // Grant target teacher.
            var target = existingRows.FirstOrDefault(x => x.TeacherUserId == teacherUserId);
            if (target != null)
            {
                if (!target.IsAllowed || target.HoduserId != hodUserId)
                {
                    target.IsAllowed = true;
                    target.HoduserId = hodUserId;
                }
            }
            else
            {
                _context.TeacherPermissions.Add(new TeacherPermission
                {
                    TeacherUserId = teacherUserId,
                    ProgramCourseId = programCourseId.Value,
                    PermissionTypeId = typeId,
                    HoduserId = hodUserId,
                    IsAllowed = true
                });
            }

            // Exclusive is for the HOD permission editor (one CLO owner per subject).
            // Allocation save is per section and must keep every granted teacher.
            if (exclusive)
            {
                foreach (var row in existingRows.Where(x =>
                             x.TeacherUserId != teacherUserId && x.IsAllowed))
                {
                    row.IsAllowed = false;
                    row.HoduserId = hodUserId;
                }
            }
        }
        else
        {
            var target = existingRows.FirstOrDefault(x => x.TeacherUserId == teacherUserId);
            if (target != null)
            {
                target.IsAllowed = false;
                target.HoduserId = hodUserId;
            }
            else
            {
                _context.TeacherPermissions.Add(new TeacherPermission
                {
                    TeacherUserId = teacherUserId,
                    ProgramCourseId = programCourseId.Value,
                    PermissionTypeId = typeId,
                    HoduserId = hodUserId,
                    IsAllowed = false
                });
            }
        }

        await _context.SaveChangesAsync();

        return new UpdateTeacherPermissionResultDto
        {
            Ok = true,
            ProgramCourseId = programCourseId.Value,
            PermissionType = permissionType,
            IsAllowed = isAllowed
        };
    }

    public bool CanUploadFolderTemplate(string templateCode, TeacherPermissionFlagsDto flags)
    {
        return templateCode switch
        {
            "b" => flags.CanManageCloPloMapping || flags.CanManageCloTaskMapping || flags.CanManageClos,
            "c" => flags.CanUploadWeeklyContent,
            "g" => flags.CanUploadMidExamQuestions || flags.CanUploadFinalExamQuestions,
            _ => true
        };
    }

    public bool CanUploadFileCategory(string fileCategory, TeacherPermissionFlagsDto flags)
    {
        if (fileCategory is "Mid Exam Question")
        {
            return flags.CanUploadMidExamQuestions;
        }

        if (fileCategory is "Final Exam Question")
        {
            return flags.CanUploadFinalExamQuestions;
        }

        if (fileCategory is "CLO-GA Mapping" or "CLO-Task Mapping")
        {
            return flags.CanManageCloPloMapping || flags.CanManageCloTaskMapping || flags.CanManageClos;
        }

        if (fileCategory is "Course Content")
        {
            return flags.CanUploadWeeklyContent;
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

    private static TeacherPermissionFlagsDto BuildFlags(
        List<TeacherPermission> permissions,
        Dictionary<string, int> typeMap)
    {
        return new TeacherPermissionFlagsDto
        {
            CanManageClos = HasAllowed(permissions, typeMap, TeacherPermissionTypes.ManageClos),
            CanUploadWeeklyContent = HasAllowed(permissions, typeMap, TeacherPermissionTypes.UploadWeeklyContent)
                || HasAllowed(permissions, typeMap, TeacherPermissionTypes.LegacyUploadCourseContent),
            CanUploadMidExamQuestions = HasAllowed(permissions, typeMap, TeacherPermissionTypes.UploadMidExamQuestions)
                || HasAllowed(permissions, typeMap, TeacherPermissionTypes.LegacyUploadExamQuestions),
            CanUploadFinalExamQuestions = HasAllowed(permissions, typeMap, TeacherPermissionTypes.UploadFinalExamQuestions)
                || HasAllowed(permissions, typeMap, TeacherPermissionTypes.LegacyUploadExamQuestions),
            CanManageCloPloMapping = HasAllowed(permissions, typeMap, TeacherPermissionTypes.ManageCloPloMapping),
            CanManageCloTaskMapping = HasAllowed(permissions, typeMap, TeacherPermissionTypes.ManageCloTaskMapping)
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

    public static bool IsSectionA(string? sectionName) =>
        SectionNameFormatter.IsSectionA(sectionName);

    private sealed class OfferedCoursePermissionContext
    {
        public int ProgramCourseId { get; set; }
        public int ProgramId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
    }
}

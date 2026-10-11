using System.Security.Claims;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class GraderAccessService
{
    private static int _tableReady;
    private readonly FolderManagementDbContext _context;

    public GraderAccessService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public static int? GetUserId(ClaimsPrincipal? user)
    {
        if (user == null)
        {
            return null;
        }

        var raw = user.FindFirstValue(JwtTokenService.UserIdClaim)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(ClaimTypes.Name);
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    public static bool IsGrader(ClaimsPrincipal? user)
    {
        if (user == null)
        {
            return false;
        }

        var role = user.FindFirstValue(JwtTokenService.RoleClaim)
            ?? user.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty;
        return role.Equals("Grader", StringComparison.OrdinalIgnoreCase);
    }

    public async Task EnsureTableAsync()
    {
        if (Interlocked.CompareExchange(ref _tableReady, 1, 0) != 0)
        {
            return;
        }

        await _context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.GraderCourseTasks', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GraderCourseTasks
                (
                    GraderCourseTaskID INT IDENTITY(1, 1) NOT NULL
                        CONSTRAINT PK_GraderCourseTasks PRIMARY KEY,
                    GraderAllocationID INT NOT NULL,
                    OfferedCourseID INT NOT NULL,
                    TaskCode NVARCHAR(50) NOT NULL,
                    AssignedByUserID INT NOT NULL,
                    AssignedAt DATETIME NOT NULL
                        CONSTRAINT DF_GraderCourseTasks_AssignedAt DEFAULT (GETDATE()),
                    CONSTRAINT UQ_GraderCourseTasks UNIQUE (GraderAllocationID, OfferedCourseID, TaskCode),
                    CONSTRAINT FK_GraderCourseTasks_Alloc FOREIGN KEY (GraderAllocationID)
                        REFERENCES dbo.GraderAllocations (GraderAllocationID) ON DELETE CASCADE,
                CONSTRAINT FK_GraderCourseTasks_OC FOREIGN KEY (OfferedCourseID)
                    REFERENCES dbo.OfferedCourses (OfferedCourseID)
                );
            END
            IF COL_LENGTH('dbo.GraderCourseTasks', 'TaskTitle') IS NULL
                ALTER TABLE dbo.GraderCourseTasks ADD TaskTitle NVARCHAR(200) NULL;
            """);
    }

    public async Task<bool> CanActAsTeacherAsync(int graderUserId, int teacherUserId)
    {
        if (graderUserId <= 0 || teacherUserId <= 0)
        {
            return false;
        }

        return await _context.GraderAllocations
            .AsNoTracking()
            .AnyAsync(g => g.GraderUserId == graderUserId && g.TeacherUserId == teacherUserId);
    }

    public async Task<bool> CanAccessAsync(int graderUserId, int offeredCourseId, string taskCode)
    {
        await EnsureTableAsync();
        var code = GraderTaskCodes.Normalize(taskCode);
        if (!GraderTaskCodes.IsValid(code))
        {
            return false;
        }

        return await (
            from task in _context.GraderCourseTasks.AsNoTracking()
            join alloc in _context.GraderAllocations.AsNoTracking()
                on task.GraderAllocationId equals alloc.GraderAllocationId
            join oc in _context.OfferedCourses.AsNoTracking()
                on task.OfferedCourseId equals oc.OfferedCourseId
            where alloc.GraderUserId == graderUserId
                && task.OfferedCourseId == offeredCourseId
                && task.TaskCode == code
                && oc.TeacherUserId == alloc.TeacherUserId
            select task.GraderCourseTaskId).AnyAsync();
    }

    public async Task<string?> MarksTaskCodeForAssessmentAsync(int assessmentId)
    {
        var typeName = await _context.Assessments
            .AsNoTracking()
            .Where(a => a.AssessmentId == assessmentId)
            .Select(a => a.AssessmentType.AssessmentTypeName)
            .FirstOrDefaultAsync();

        return typeName switch
        {
            CourseResultService.AssignmentType => GraderTaskCodes.AssignmentMarks,
            CourseResultService.QuizType => GraderTaskCodes.QuizMarks,
            _ => null
        };
    }

    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, int offeredCourseId, string taskCode)
    {
        if (!IsGrader(user))
        {
            return true;
        }

        var graderId = GetUserId(user);
        return graderId.HasValue && await CanAccessAsync(graderId.Value, offeredCourseId, taskCode);
    }

    public async Task DenyIfGraderLacksTaskAsync(ClaimsPrincipal user, int offeredCourseId, string taskCode)
    {
        if (!IsGrader(user))
        {
            return;
        }

        if (!await CanAccessAsync(user, offeredCourseId, taskCode))
        {
            throw new InvalidOperationException("This task is not assigned to you for this course.");
        }
    }

    public static List<GraderTaskCatalogItemDto> Catalog() =>
        GraderTaskCodes.All
            .Select(code => new GraderTaskCatalogItemDto
            {
                TaskCode = code,
                Label = GraderTaskCodes.Label(code)
            })
            .ToList();

    public async Task<List<GraderAssignedCourseDto>> GetMineAsync(int graderUserId)
    {
        await EnsureTableAsync();
        var rows = await (
            from task in _context.GraderCourseTasks.AsNoTracking()
            join alloc in _context.GraderAllocations.AsNoTracking()
                on task.GraderAllocationId equals alloc.GraderAllocationId
            join oc in _context.OfferedCourses.AsNoTracking()
                on task.OfferedCourseId equals oc.OfferedCourseId
            join course in _context.Courses.AsNoTracking()
                on oc.CourseId equals course.CourseId
            join section in _context.Sections.AsNoTracking()
                on oc.SectionId equals section.SectionId
            join teacher in _context.Users.AsNoTracking()
                on alloc.TeacherUserId equals teacher.UserId
            where alloc.GraderUserId == graderUserId
            select new
            {
                task.GraderAllocationId,
                task.OfferedCourseId,
                task.TaskCode,
                task.TaskTitle,
                course.CourseTitle,
                CourseCode = course.CourseCode ?? string.Empty,
                section.SectionName,
                TeacherUserId = alloc.TeacherUserId,
                TeacherName = teacher.FullName,
                TeacherEmail = teacher.Email ?? string.Empty
            }).ToListAsync();

        var result = new List<GraderAssignedCourseDto>();
        foreach (var group in rows.GroupBy(x => (x.GraderAllocationId, x.OfferedCourseId)))
        {
            var sample = group.First();
            var assigned = group
                .Select(x => (x.TaskCode, x.TaskTitle))
                .ToList();
            result.Add(new GraderAssignedCourseDto
            {
                GraderAllocationId = sample.GraderAllocationId,
                OfferedCourseId = sample.OfferedCourseId,
                CourseTitle = sample.CourseTitle,
                CourseCode = sample.CourseCode,
                SectionName = sample.SectionName,
                TeacherUserId = sample.TeacherUserId,
                TeacherName = sample.TeacherName,
                TeacherEmail = sample.TeacherEmail,
                Tasks = await BuildProgressAsync(sample.OfferedCourseId, assigned, includeUnassignedSystem: false)
            });
        }

        return result
            .OrderBy(x => x.TeacherName)
            .ThenBy(x => x.CourseTitle)
            .ThenBy(x => x.SectionName)
            .ToList();
    }

    public async Task<TeacherGraderTasksPageDto> GetTeacherPageAsync(int teacherUserId)
    {
        await EnsureTableAsync();
        var allocations = await _context.GraderAllocations
            .AsNoTracking()
            .Include(g => g.GraderUser)
            .Where(g => g.TeacherUserId == teacherUserId)
            .OrderBy(g => g.GraderUser.FullName)
            .ToListAsync();

        var courses = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join course in _context.Courses.AsNoTracking() on oc.CourseId equals course.CourseId
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            where oc.TeacherUserId == teacherUserId
            select new
            {
                oc.OfferedCourseId,
                course.CourseTitle,
                CourseCode = course.CourseCode ?? string.Empty,
                section.SectionName
            }).ToListAsync();

        var saved = allocations.Count == 0
            ? new List<GraderCourseTask>()
            : await _context.GraderCourseTasks
                .AsNoTracking()
                .Where(t => allocations.Select(a => a.GraderAllocationId).Contains(t.GraderAllocationId))
                .ToListAsync();

        var teacher = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == teacherUserId);

        var page = new TeacherGraderTasksPageDto { Catalog = Catalog() };
        foreach (var allocation in allocations)
        {
            var workspace = new TeacherGraderWorkspaceDto
            {
                GraderAllocationId = allocation.GraderAllocationId,
                GraderUserId = allocation.GraderUserId,
                GraderName = allocation.GraderUser.FullName,
                GraderEmail = allocation.GraderUser.Email ?? string.Empty
            };

            foreach (var course in courses.OrderBy(c => c.CourseTitle).ThenBy(c => c.SectionName))
            {
                var assigned = saved
                    .Where(t => t.GraderAllocationId == allocation.GraderAllocationId
                        && t.OfferedCourseId == course.OfferedCourseId)
                    .Select(t => (t.TaskCode, t.TaskTitle))
                    .ToList();

                workspace.Courses.Add(new GraderAssignedCourseDto
                {
                    GraderAllocationId = allocation.GraderAllocationId,
                    OfferedCourseId = course.OfferedCourseId,
                    CourseTitle = course.CourseTitle,
                    CourseCode = course.CourseCode,
                    SectionName = course.SectionName,
                    TeacherUserId = teacherUserId,
                    TeacherName = teacher?.FullName ?? string.Empty,
                    TeacherEmail = teacher?.Email ?? string.Empty,
                    Tasks = await BuildProgressAsync(course.OfferedCourseId, assigned, includeUnassignedSystem: true)
                });
            }

            page.Graders.Add(workspace);
        }

        return page;
    }

    public async Task SaveAsync(int teacherUserId, SaveGraderTasksDto dto)
    {
        await EnsureTableAsync();
        var allocation = await _context.GraderAllocations
            .FirstOrDefaultAsync(g => g.GraderAllocationId == dto.GraderAllocationId);
        if (allocation == null || allocation.TeacherUserId != teacherUserId)
        {
            throw new InvalidOperationException("This grader is not allocated to you.");
        }

        var offered = await _context.OfferedCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(oc => oc.OfferedCourseId == dto.OfferedCourseId);
        if (offered == null || offered.TeacherUserId != teacherUserId)
        {
            throw new InvalidOperationException("You can only assign tasks on your own courses.");
        }

        var incoming = (dto.Tasks ?? new List<SaveGraderTaskItemDto>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Title) || GraderTaskCodes.IsValid(t.TaskCode))
            .ToList();
        if (incoming.Count == 0 && dto.TaskCodes is { Count: > 0 })
        {
            incoming = dto.TaskCodes
                .Select(code => new SaveGraderTaskItemDto
                {
                    TaskCode = code,
                    Title = GraderTaskCodes.Label(code)
                })
                .ToList();
        }

        var wanted = new List<(string Code, string Title)>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in incoming)
        {
            var title = (item.Title ?? string.Empty).Trim();
            if (GraderTaskCodes.IsValid(item.TaskCode))
            {
                var code = GraderTaskCodes.Normalize(item.TaskCode);
                if (usedCodes.Add(code))
                {
                    wanted.Add((code, GraderTaskCodes.Label(code)));
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var customCode = string.IsNullOrWhiteSpace(item.TaskCode) || GraderTaskCodes.IsValid(item.TaskCode)
                ? CustomTaskCode(title, usedCodes)
                : item.TaskCode.Trim();
            if (customCode.Length > 50)
            {
                customCode = customCode[..50];
            }

            if (usedCodes.Add(customCode))
            {
                wanted.Add((customCode, title.Length > 200 ? title[..200] : title));
            }
        }

        var existing = await _context.GraderCourseTasks
            .Where(t => t.GraderAllocationId == dto.GraderAllocationId
                && t.OfferedCourseId == dto.OfferedCourseId)
            .ToListAsync();

        var wantedCodes = wanted.Select(w => w.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing.Where(r => !wantedCodes.Contains(r.TaskCode)).ToList())
        {
            _context.GraderCourseTasks.Remove(row);
        }

        foreach (var item in wanted)
        {
            var row = existing.FirstOrDefault(r =>
                r.TaskCode.Equals(item.Code, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                _context.GraderCourseTasks.Add(new GraderCourseTask
                {
                    GraderAllocationId = dto.GraderAllocationId,
                    OfferedCourseId = dto.OfferedCourseId,
                    TaskCode = item.Code,
                    TaskTitle = item.Title,
                    AssignedByUserId = teacherUserId,
                    AssignedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.TaskTitle = item.Title;
            }
        }

        await _context.SaveChangesAsync();
    }

    private static string CustomTaskCode(string title, HashSet<string> used)
    {
        var slug = new string(title.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "Task";
        }

        if (slug.Length > 24)
        {
            slug = slug[..24];
        }

        var code = "Custom-" + slug;
        var n = 2;
        while (used.Contains(code))
        {
            code = "Custom-" + slug + n;
            n++;
        }

        return code.Length > 50 ? code[..50] : code;
    }

    private async Task<List<GraderTaskProgressDto>> BuildProgressAsync(
        int offeredCourseId,
        IReadOnlyList<(string TaskCode, string? TaskTitle)> assigned,
        bool includeUnassignedSystem)
    {
        var assignedCodes = assigned
            .Select(x => x.TaskCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<GraderTaskProgressDto>();
        foreach (var code in GraderTaskCodes.All)
        {
            var isAssigned = assignedCodes.Contains(code);
            if (!isAssigned && !includeUnassignedSystem)
            {
                continue;
            }

            var (done, total, text) = isAssigned
                ? await ComputeProgressAsync(offeredCourseId, code)
                : (0, 0, "Not allowed yet");

            list.Add(new GraderTaskProgressDto
            {
                TaskCode = code,
                Label = GraderTaskCodes.Label(code),
                Assigned = isAssigned,
                Done = done,
                Total = total,
                ProgressText = text
            });
        }

        foreach (var custom in assigned.Where(x => !GraderTaskCodes.IsValid(x.TaskCode)))
        {
            list.Add(new GraderTaskProgressDto
            {
                TaskCode = custom.TaskCode,
                Label = string.IsNullOrWhiteSpace(custom.TaskTitle) ? custom.TaskCode : custom.TaskTitle,
                Assigned = true,
                ProgressText = "Allowed"
            });
        }

        return list;
    }

    private async Task<(int Done, int Total, string Text)> ComputeProgressAsync(
        int offeredCourseId,
        string taskCode)
    {
        if (taskCode == GraderTaskCodes.Attendance)
        {
            var dates = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(r => r.OfferedCourseId == offeredCourseId)
                .Select(r => r.AttendanceDate)
                .Distinct()
                .CountAsync();
            return dates == 0
                ? (0, 0, "No dates yet")
                : (dates, dates, $"{dates} date{(dates == 1 ? "" : "s")} recorded");
        }

        if (taskCode == GraderTaskCodes.ComponentMarks)
        {
            var saved = await _context.StudentComponentMarks
                .AsNoTracking()
                .AnyAsync(x => x.OfferedCourseId == offeredCourseId)
                || await _context.OfferedCourseHomeworkMarkSchemes
                    .AsNoTracking()
                    .AnyAsync(x => x.OfferedCourseId == offeredCourseId);
            return saved ? (1, 1, "Saved") : (0, 1, "Not saved");
        }

        var typeName = taskCode == GraderTaskCodes.QuizMarks
            ? CourseResultService.QuizType
            : CourseResultService.AssignmentType;

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Where(a => a.OfferedCourseId == offeredCourseId
                && a.AssessmentType.AssessmentTypeName == typeName)
            .Select(a => a.AssessmentId)
            .ToListAsync();

        var enrolled = await _context.StudentOfferedCourses
            .AsNoTracking()
            .CountAsync(s => s.OfferedCourseId == offeredCourseId);
        if (assessments.Count == 0)
        {
            return (0, 0, $"No {typeName.ToLowerInvariant()}s yet");
        }

        if (enrolled == 0)
        {
            return (0, assessments.Count, $"0/{assessments.Count} {typeName.ToLowerInvariant()} sheets");
        }

        var markedByAssessment = await _context.StudentAssessmentMarks
            .AsNoTracking()
            .Where(m => assessments.Contains(m.AssessmentId))
            .GroupBy(m => m.AssessmentId)
            .Select(g => new { AssessmentId = g.Key, Count = g.Select(x => x.StudentId).Distinct().Count() })
            .ToListAsync();

        var done = markedByAssessment.Count(x => x.Count >= enrolled);
        var noun = typeName.Equals("Quiz", StringComparison.OrdinalIgnoreCase) ? "quiz" : "assignment";
        return (done, assessments.Count, $"{done}/{assessments.Count} {noun} sheets");
    }
}

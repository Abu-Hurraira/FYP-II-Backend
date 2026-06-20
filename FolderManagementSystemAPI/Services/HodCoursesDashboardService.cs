using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Services;

public class HodCoursesDashboardService
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly string _connectionString;

    public HodCoursesDashboardService(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    public async Task<HodCoursesDashboardDto> BuildAsync()
    {
        var submittedIds = await _context.CourseFolders
            .AsNoTracking()
            .Where(cf => cf.FolderStatus == "Submitted")
            .Select(cf => cf.OfferedCourseId)
            .ToListAsync();

        var submittedSet = submittedIds.ToHashSet();

        var offeredCourses = await _context.OfferedCourses
            .AsNoTracking()
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .Where(oc => !submittedSet.Contains(oc.OfferedCourseId))
            .OrderBy(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ThenBy(oc => oc.TeacherUser.FullName)
            .ToListAsync();

        if (offeredCourses.Count == 0)
        {
            return new HodCoursesDashboardDto();
        }

        var programCourseKeys = offeredCourses
            .Select(x => (x.Section.Session.ProgramId, x.CourseId))
            .Distinct()
            .ToList();

        var programCourseIdMap = await LoadProgramCourseIdsAsync(programCourseKeys);
        var comparablesByKey = await LoadComparablesBulkAsync(programCourseKeys, submittedSet);
        var leafTopicsByProgramCourse = await LoadLeafTopicsBulkAsync(programCourseIdMap.Values);
        var allComparableIds = comparablesByKey.Values.SelectMany(x => x).Distinct().ToList();
        var progressMap = await LoadProgressMapBulkAsync(allComparableIds);
        var permissionGroupsByCourse = await _permissionService.BuildPermissionGroupsBulkAsync(
            offeredCourses,
            programCourseIdMap);

        var blocks = new List<HodCourseDashboardBlockDto>();

        foreach (var courseGroup in offeredCourses.GroupBy(x => x.CourseId))
        {
            var sample = courseGroup.First().Course;
            var sections = courseGroup
                .Select(offering => BuildSectionProgress(
                    offering,
                    programCourseIdMap,
                    comparablesByKey,
                    leafTopicsByProgramCourse,
                    progressMap))
                .ToList();

            blocks.Add(new HodCourseDashboardBlockDto
            {
                CourseId = courseGroup.Key,
                CourseTitle = sample.CourseTitle,
                CourseCode = sample.CourseCode,
                ClassCreditHours = sample.ClassCreditHours,
                LabCreditHours = sample.LabCreditHours,
                SectionCount = courseGroup.Select(x => x.SectionId).Distinct().Count(),
                TeacherCount = courseGroup.Select(x => x.TeacherUserId).Distinct().Count(),
                PermissionGroups = permissionGroupsByCourse.GetValueOrDefault(courseGroup.Key) ?? new(),
                Sections = sections
            });
        }

        return new HodCoursesDashboardDto { Courses = blocks };
    }

    private static HodSectionProgressDto BuildSectionProgress(
        OfferedCourse oc,
        Dictionary<(int ProgramId, int CourseId), int> programCourseIdMap,
        Dictionary<(int ProgramId, int CourseId), List<int>> comparablesByKey,
        Dictionary<int, List<int>> leafTopicsByProgramCourse,
        Dictionary<string, bool> progressMap)
    {
        var programId = oc.Section.Session.ProgramId;
        var key = (programId, oc.CourseId);
        var comparableIds = comparablesByKey.GetValueOrDefault(key) ?? new List<int> { oc.OfferedCourseId };

        programCourseIdMap.TryGetValue(key, out var programCourseId);
        var leafTopicIds = programCourseId > 0
            ? leafTopicsByProgramCourse.GetValueOrDefault(programCourseId) ?? new List<int>()
            : new List<int>();

        var metrics = CourseSyncCalculator.Compute(
            oc.OfferedCourseId,
            leafTopicIds,
            comparableIds,
            progressMap);

        return new HodSectionProgressDto
        {
            OfferedCourseId = oc.OfferedCourseId,
            CourseId = oc.CourseId,
            ProgramId = programId,
            TeacherUserId = oc.TeacherUserId,
            TeacherName = oc.TeacherUser.FullName,
            SectionName = oc.Section.SectionName,
            ProgramName = oc.Section.Session.Program.ProgramName,
            IsSectionA = IsSectionAName(oc.Section.SectionName),
            HasWeeklyContent = metrics.HasWeeklyContent,
            TotalTopics = metrics.TotalTopics,
            CoveredTopics = metrics.CoveredTopics,
            SyncScore = metrics.SyncScore,
            ProgressPercentage = metrics.ProgressPercentage,
            GapTopics = metrics.GapTopics,
            DeviationLabel = metrics.DeviationLabel,
            ShowActionButtons = metrics.ShowActionButtons
        };
    }

    private async Task<Dictionary<(int ProgramId, int CourseId), int>> LoadProgramCourseIdsAsync(
        List<(int ProgramId, int CourseId)> keys)
    {
        var map = new Dictionary<(int ProgramId, int CourseId), int>();

        if (keys.Count == 0)
        {
            return map;
        }

        var programIds = keys.Select(k => k.ProgramId).Distinct().ToList();
        var courseIds = keys.Select(k => k.CourseId).Distinct().ToList();
        var keySet = keys.ToHashSet();

        var rows = await _context.AcademicProgramCourses
            .AsNoTracking()
            .Where(apc => programIds.Contains(apc.ProgramId) && courseIds.Contains(apc.CourseId))
            .Select(apc => new { apc.ProgramId, apc.CourseId, apc.ProgramCourseId })
            .ToListAsync();

        foreach (var row in rows)
        {
            var key = (row.ProgramId, row.CourseId);
            if (keySet.Contains(key))
            {
                map[key] = row.ProgramCourseId;
            }
        }

        return map;
    }

    private async Task<Dictionary<(int ProgramId, int CourseId), List<int>>> LoadComparablesBulkAsync(
        List<(int ProgramId, int CourseId)> keys,
        HashSet<int> submittedSet)
    {
        var map = keys.ToDictionary(k => k, _ => new List<int>());

        if (keys.Count == 0)
        {
            return map;
        }

        var programIds = keys.Select(k => k.ProgramId).Distinct().ToList();
        var courseIds = keys.Select(k => k.CourseId).Distinct().ToList();
        var keySet = keys.ToHashSet();

        var rows = await _context.OfferedCourses
            .AsNoTracking()
            .Where(oc => courseIds.Contains(oc.CourseId))
            .Where(oc => programIds.Contains(oc.Section.Session.ProgramId))
            .Select(oc => new
            {
                oc.OfferedCourseId,
                oc.CourseId,
                ProgramId = oc.Section.Session.ProgramId
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            if (submittedSet.Contains(row.OfferedCourseId))
            {
                continue;
            }

            var key = (row.ProgramId, row.CourseId);
            if (keySet.Contains(key))
            {
                map[key].Add(row.OfferedCourseId);
            }
        }

        return map;
    }

    private async Task<Dictionary<int, List<int>>> LoadLeafTopicsBulkAsync(IEnumerable<int> programCourseIds)
    {
        var map = new Dictionary<int, List<int>>();
        var ids = programCourseIds.Distinct().Where(id => id > 0).ToList();

        if (ids.Count == 0)
        {
            return map;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var idList = string.Join(",", ids);
        var command = new SqlCommand($@"
            SELECT t.ProgramCourseID, t.TopicID
            FROM CourseTopics t
            WHERE t.ProgramCourseID IN ({idList})
            AND NOT EXISTS (
                SELECT 1 FROM CourseTopics child WHERE child.ParentTopicID = t.TopicID
            );
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var programCourseId = Convert.ToInt32(reader["ProgramCourseID"]);
            var topicId = Convert.ToInt32(reader["TopicID"]);

            if (!map.TryGetValue(programCourseId, out var topics))
            {
                topics = new List<int>();
                map[programCourseId] = topics;
            }

            topics.Add(topicId);
        }

        return map;
    }

    private async Task<Dictionary<string, bool>> LoadProgressMapBulkAsync(List<int> offeredCourseIds)
    {
        var map = new Dictionary<string, bool>();

        if (offeredCourseIds.Count == 0)
        {
            return map;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var idList = string.Join(",", offeredCourseIds.Distinct());
        var command = new SqlCommand($@"
            SELECT OfferedCourseID, TopicID, IsCompleted
            FROM OfferedCourseTopicProgress
            WHERE OfferedCourseID IN ({idList});
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var offeredCourseId = Convert.ToInt32(reader["OfferedCourseID"]);
            var topicId = Convert.ToInt32(reader["TopicID"]);
            var completed = Convert.ToBoolean(reader["IsCompleted"]);
            map[$"{offeredCourseId}|{topicId}"] = completed;
        }

        return map;
    }

    private static bool IsSectionAName(string? sectionName)
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

        return string.Equals(name, "A", StringComparison.OrdinalIgnoreCase);
    }
}

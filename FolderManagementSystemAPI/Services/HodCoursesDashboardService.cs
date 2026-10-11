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
        var offeredCourses = await LoadActiveOfferingsAsync();
        if (offeredCourses.Count == 0)
        {
            return new HodCoursesDashboardDto();
        }

        return new HodCoursesDashboardDto
        {
            Courses = await BuildBlocksAsync(offeredCourses, includeDetails: true)
        };
    }

    /// <summary>
    /// Fast first paint: course headers + counts + name lists only (no permissions / sync metrics).
    /// </summary>
    public async Task<HodCoursesDashboardDto> BuildSummaryAsync()
    {
        var offeredCourses = await LoadActiveOfferingsAsync();
        if (offeredCourses.Count == 0)
        {
            return new HodCoursesDashboardDto();
        }

        return new HodCoursesDashboardDto
        {
            Courses = await BuildBlocksAsync(offeredCourses, includeDetails: false)
        };
    } 

    /// <summary>
    /// Full permissions + section progress for one course (loaded when HOD expands a row).
    /// </summary>
    public async Task<HodCourseDashboardBlockDto?> BuildCourseDetailAsync(int courseId)
    {
        var offeredCourses = await LoadActiveOfferingsAsync(courseId);
        if (offeredCourses.Count == 0)
        {
            return null;
        }

        var blocks = await BuildBlocksAsync(offeredCourses, includeDetails: true);
        return blocks.FirstOrDefault();
    }

    private async Task<List<OfferedCourse>> LoadActiveOfferingsAsync(int? courseId = null)
    {
        var query = _context.OfferedCourses
            .AsNoTracking()
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(oc => oc.CourseId == courseId.Value);
        }

        return await query
            .OrderBy(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ThenBy(oc => oc.TeacherUser.FullName)
            .ToListAsync();
    }

    private async Task<List<HodCourseDashboardBlockDto>> BuildBlocksAsync(
        List<OfferedCourse> offeredCourses,
        bool includeDetails)
    {
        var programCourseKeys = offeredCourses
            .Select(x => (x.Section.Session.ProgramId, x.CourseId))
            .Distinct()
            .ToList();

        var programCourseIdMap = await LoadProgramCourseIdsAsync(programCourseKeys);

        Dictionary<int, List<HodTeacherPermissionGroupDto>> permissionGroupsByCourse = new();
        Dictionary<(int ProgramId, int CourseId, int SessionId), List<int>> comparablesByKey = new();
        Dictionary<(int ProgramCourseId, int SessionId), List<int>> leafTopicsByProgramCourseSession = new();
        Dictionary<string, bool> progressMap = new();

        if (includeDetails)
        {
            var comparableKeys = offeredCourses
                .Select(x => (x.Section.Session.ProgramId, x.CourseId, x.Section.SessionId))
                .Distinct()
                .ToList();

            comparablesByKey = await LoadComparablesBulkAsync(comparableKeys);

            var programCourseSessionKeys = offeredCourses
                .Select(oc =>
                {
                    var programId = oc.Section.Session.ProgramId;
                    programCourseIdMap.TryGetValue((programId, oc.CourseId), out var pcId);
                    return (pcId, oc.Section.SessionId);
                })
                .Where(x => x.pcId > 0)
                .Distinct()
                .ToList();

            leafTopicsByProgramCourseSession = await LoadLeafTopicsBulkAsync(programCourseSessionKeys);
            var allComparableIds = comparablesByKey.Values
                .SelectMany(x => x)
                .Concat(offeredCourses.Select(x => x.OfferedCourseId))
                .Distinct()
                .ToList();
            progressMap = await LoadProgressMapBulkAsync(allComparableIds);
            permissionGroupsByCourse = await _permissionService.BuildPermissionGroupsBulkAsync(
                offeredCourses,
                programCourseIdMap);
        }

        var blocks = new List<HodCourseDashboardBlockDto>();

        foreach (var courseGroup in offeredCourses.GroupBy(x => x.CourseId))
        {
            var sample = courseGroup.First().Course;
            var teacherNames = courseGroup
                .Select(x => x.TeacherUser.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();
            var sectionNames = courseGroup
                .Select(x => x.Section.SectionName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            var block = new HodCourseDashboardBlockDto
            {
                CourseId = courseGroup.Key,
                CourseTitle = sample.CourseTitle,
                CourseCode = sample.CourseCode,
                ClassCreditHours = sample.ClassCreditHours,
                LabCreditHours = sample.LabCreditHours,
                SectionCount = courseGroup.Select(x => x.SectionId).Distinct().Count(),
                TeacherCount = courseGroup.Select(x => x.TeacherUserId).Distinct().Count(),
                TeacherNames = teacherNames,
                SectionNames = sectionNames,
                DetailsLoaded = includeDetails,
                ProgramIds = courseGroup
                    .Select(x => x.Section.Session.ProgramId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList()
            };

            if (includeDetails)
            {
                block.PermissionGroups =
                    permissionGroupsByCourse.GetValueOrDefault(courseGroup.Key) ?? new();
                block.Sections = courseGroup
                    .Select(offering => BuildSectionProgress(
                        offering,
                        programCourseIdMap,
                        comparablesByKey,
                        leafTopicsByProgramCourseSession,
                        progressMap))
                    .ToList();
            }

            blocks.Add(block);
        }

        return blocks;
    }

    private static HodSectionProgressDto BuildSectionProgress(
        OfferedCourse oc,
        Dictionary<(int ProgramId, int CourseId), int> programCourseIdMap,
        Dictionary<(int ProgramId, int CourseId, int SessionId), List<int>> comparablesByKey,
        Dictionary<(int ProgramCourseId, int SessionId), List<int>> leafTopicsByProgramCourseSession,
        Dictionary<string, bool> progressMap)
    {
        var programId = oc.Section.Session.ProgramId;
        var sessionId = oc.Section.SessionId;
        var key = (programId, oc.CourseId, sessionId);
        var comparableIds = comparablesByKey.GetValueOrDefault(key) ?? new List<int> { oc.OfferedCourseId };
        if (!comparableIds.Contains(oc.OfferedCourseId))
        {
            comparableIds = comparableIds.Append(oc.OfferedCourseId).ToList();
        }

        programCourseIdMap.TryGetValue((programId, oc.CourseId), out var programCourseId);
        var leafTopicIds = programCourseId > 0
            ? leafTopicsByProgramCourseSession.GetValueOrDefault((programCourseId, sessionId)) ?? new List<int>()
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
            ProgramCourseId = programCourseId,
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

    private async Task<Dictionary<(int ProgramId, int CourseId, int SessionId), List<int>>> LoadComparablesBulkAsync(
        List<(int ProgramId, int CourseId, int SessionId)> keys)
    {
        var map = keys.ToDictionary(k => k, _ => new List<int>());

        if (keys.Count == 0)
        {
            return map;
        }

        var programIds = keys.Select(k => k.ProgramId).Distinct().ToList();
        var courseIds = keys.Select(k => k.CourseId).Distinct().ToList();
        var sessionIds = keys.Select(k => k.SessionId).Distinct().ToList();
        var keySet = keys.ToHashSet();

        var rows = await _context.OfferedCourses
            .AsNoTracking()
            .Where(oc => courseIds.Contains(oc.CourseId))
            .Where(oc => programIds.Contains(oc.Section.Session.ProgramId))
            .Where(oc => sessionIds.Contains(oc.Section.SessionId))
            .Select(oc => new
            {
                oc.OfferedCourseId,
                oc.CourseId,
                ProgramId = oc.Section.Session.ProgramId,
                SessionId = oc.Section.SessionId
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            var key = (row.ProgramId, row.CourseId, row.SessionId);
            if (keySet.Contains(key))
            {
                map[key].Add(row.OfferedCourseId);
            }
        }

        return map;
    }

    private async Task<Dictionary<(int ProgramCourseId, int SessionId), List<int>>> LoadLeafTopicsBulkAsync(
        IEnumerable<(int ProgramCourseId, int SessionId)> keys)
    {
        var map = new Dictionary<(int ProgramCourseId, int SessionId), List<int>>();
        var keyList = keys
            .Where(k => k.ProgramCourseId > 0 && k.SessionId > 0)
            .Distinct()
            .ToList();

        if (keyList.Count == 0)
        {
            return map;
        }

        var programCourseIds = keyList.Select(k => k.ProgramCourseId).Distinct().ToList();
        var sessionIds = keyList.Select(k => k.SessionId).Distinct().ToList();
        var keySet = keyList.ToHashSet();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var pcList = string.Join(",", programCourseIds);
        var sessionList = string.Join(",", sessionIds);
        var command = new SqlCommand($@"
            SELECT t.ProgramCourseID, t.SessionID, t.TopicID
            FROM CourseTopics t
            WHERE t.ProgramCourseID IN ({pcList})
            AND t.SessionID IN ({sessionList})
            AND NOT EXISTS (
                SELECT 1
                FROM CourseTopics child
                WHERE child.ParentTopicID = t.TopicID
                AND child.SessionID = t.SessionID
            );
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var programCourseId = Convert.ToInt32(reader["ProgramCourseID"]);
            var sessionId = Convert.ToInt32(reader["SessionID"]);
            var topicId = Convert.ToInt32(reader["TopicID"]);
            var key = (programCourseId, sessionId);

            if (!keySet.Contains(key))
            {
                continue;
            }

            if (!map.TryGetValue(key, out var topics))
            {
                topics = new List<int>();
                map[key] = topics;
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

    private static bool IsSectionAName(string? sectionName) =>
        SectionNameFormatter.IsSectionA(sectionName);
}

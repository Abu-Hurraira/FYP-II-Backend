using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodDashboardController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly HodCoursesDashboardService _dashboardService;
    private readonly string _connectionString;

    public HodDashboardController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        HodCoursesDashboardService dashboardService)
    {
        _context = context;
        _permissionService = permissionService;
        _dashboardService = dashboardService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    [HttpGet("{hodUserId:int}/courses")]
    public async Task<ActionResult<IEnumerable<HodCourseSummaryDto>>> GetHodCourses(int hodUserId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        var summaries = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .GroupBy(oc => new
            {
                oc.CourseId,
                oc.Course.CourseTitle,
                oc.Course.CourseCode,
                oc.Course.ClassCreditHours,
                oc.Course.LabCreditHours
            })
            .Select(group => new HodCourseSummaryDto
            {
                CourseId = group.Key.CourseId,
                CourseTitle = group.Key.CourseTitle,
                CourseCode = group.Key.CourseCode,
                ClassCreditHours = group.Key.ClassCreditHours,
                LabCreditHours = group.Key.LabCreditHours,
                SectionCount = group.Select(x => x.SectionId).Distinct().Count(),
                TeacherCount = group.Select(x => x.TeacherUserId).Distinct().Count(),
                OfferingCount = group.Count()
            })
            .OrderBy(x => x.CourseTitle)
            .ToListAsync();

        return Ok(summaries);
    }

    [HttpGet("{hodUserId:int}/courses-dashboard")]
    public async Task<ActionResult<HodCoursesDashboardDto>> GetCoursesDashboard(int hodUserId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        var dashboard = await _dashboardService.BuildAsync();
        return Ok(dashboard);
    }

    [HttpGet("{hodUserId:int}/courses/{courseId:int}/offerings")]
    public async Task<ActionResult<IEnumerable<HodCourseCardDto>>> GetHodCourseOfferings(
        int hodUserId,
        int courseId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        var courseExists = await _context.Courses.AnyAsync(c => c.CourseId == courseId);

        if (!courseExists)
        {
            return NotFound(new { message = "Course not found." });
        }

        var offeredCourses = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.CourseId == courseId)
            .OrderBy(oc => oc.Section.SectionName)
            .ThenBy(oc => oc.TeacherUser.FullName)
            .ToListAsync();

        var result = new List<HodCourseCardDto>();

        foreach (var oc in offeredCourses)
        {
            var card = await BuildCourseCardAsync(oc);

            if (string.Equals(card.StatusLabel, "Submitted", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(card);
        }

        return Ok(result);
    }

    [HttpGet("{hodUserId:int}/submitted-folders")]
    public async Task<ActionResult<IEnumerable<HodSubmittedReviewDto>>> GetSubmittedFoldersForReview(int hodUserId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        var submittedOfferings = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .Where(oc => _context.CourseFolders.Any(cf =>
                cf.OfferedCourseId == oc.OfferedCourseId &&
                cf.FolderStatus == "Submitted"))
            .OrderByDescending(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ToListAsync();

        var result = new List<HodSubmittedReviewDto>();

        foreach (var oc in submittedOfferings)
        {
            var folder = await GetCourseFolderInfoAsync(oc.OfferedCourseId);
            var submittedAt = folder != null
                ? await GetLatestSubmissionDateAsync(folder.CourseFolderId)
                : null;
            var progressPercent = folder != null
                ? await GetFolderProgressPercentAsync(folder.CourseFolderId)
                : 0;

            result.Add(new HodSubmittedReviewDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                CourseId = oc.CourseId,
                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,
                SectionName = oc.Section.SectionName,
                ProgramName = oc.Section.Session.Program.ProgramName,
                SubmittedAt = submittedAt,
                OverallProgressPercent = progressPercent
            });
        }

        result = result
            .OrderByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
            .ThenBy(x => x.CourseTitle)
            .ToList();

        return Ok(result);
    }

    private async Task<bool> IsValidHodAsync(int hodUserId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == hodUserId && u.Role.RoleName == "HOD");
    }

    private async Task<HodCourseCardDto> BuildCourseCardAsync(OfferedCourse oc)
    {
        var programId = oc.Section.Session.ProgramId;
        var programCourseId = await EnsureProgramCourseAsync(programId, oc.CourseId);

        var comparableIds = await GetComparableOfferedCourseIdsAsync(programId, oc.CourseId);
        await EnsureProgressRowsForAllAsync(programCourseId, comparableIds);

        var leafTopicIds = await GetLeafTopicIdsAsync(programCourseId);
        var progressMap = await GetProgressMapAsync(comparableIds);
        var metrics = CourseSyncCalculator.Compute(
            oc.OfferedCourseId,
            leafTopicIds,
            comparableIds,
            progressMap);
        var folderStatus = await GetFolderStatusLabelAsync(oc.OfferedCourseId);

        var permissionFlags = await _permissionService.GetTeacherFlagsAsync(
            oc.TeacherUserId,
            programCourseId);

        return new HodCourseCardDto
        {
            OfferedCourseId = oc.OfferedCourseId,
            CourseId = oc.CourseId,
            ProgramId = programId,
            TeacherUserId = oc.TeacherUserId,
            TeacherName = oc.TeacherUser.FullName,
            CourseTitle = oc.Course.CourseTitle,
            CourseCode = oc.Course.CourseCode,
            SectionName = oc.Section.SectionName,
            ProgramName = oc.Section.Session.Program.ProgramName,
            StatusLabel = folderStatus,
            IsSectionA = IsSectionAName(oc.Section.SectionName),
            CanManageClos = permissionFlags.CanManageClos,
            CanUploadCourseContent = permissionFlags.CanUploadCourseContent,
            CanUploadExamQuestions = permissionFlags.CanUploadExamQuestions,
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

    private async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
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

    private async Task<List<int>> GetComparableOfferedCourseIdsAsync(int programId, int courseId)
    {
        var ids = new List<int>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT oc.OfferedCourseID
            FROM OfferedCourses oc
            INNER JOIN Sections s ON s.SectionID = oc.SectionID
            INNER JOIN AcademicSessions sess ON sess.SessionID = s.SessionID
            WHERE oc.CourseID = @CourseID
            AND sess.ProgramID = @ProgramID
            AND NOT EXISTS (
                SELECT 1
                FROM CourseFolders cf
                WHERE cf.OfferedCourseID = oc.OfferedCourseID
                AND cf.FolderStatus = 'Submitted'
            );
        ", connection);

        command.Parameters.AddWithValue("@CourseID", courseId);
        command.Parameters.AddWithValue("@ProgramID", programId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ids.Add(Convert.ToInt32(reader["OfferedCourseID"]));
        }

        return ids;
    }

    private async Task EnsureProgressRowsForAllAsync(int programCourseId, List<int> offeredCourseIds)
    {
        if (offeredCourseIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var offeredCourseId in offeredCourseIds)
        {
            var command = new SqlCommand(@"
                INSERT INTO OfferedCourseTopicProgress (OfferedCourseID, TopicID, IsCompleted)
                SELECT @OfferedCourseID, t.TopicID, 0
                FROM CourseTopics t
                WHERE t.ProgramCourseID = @ProgramCourseID
                AND NOT EXISTS (
                    SELECT 1 FROM OfferedCourseTopicProgress p
                    WHERE p.OfferedCourseID = @OfferedCourseID AND p.TopicID = t.TopicID
                );
            ", connection);

            command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
            command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<int> GetLeafTopicCountAsync(int programCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseTopics t
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND NOT EXISTS (
                SELECT 1 FROM CourseTopics child WHERE child.ParentTopicID = t.TopicID
            );
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetCompletedLeafTopicCountAsync(int offeredCourseId, int programCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseTopics t
            INNER JOIN OfferedCourseTopicProgress p
                ON p.TopicID = t.TopicID AND p.OfferedCourseID = @OfferedCourseID
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND p.IsCompleted = 1
            AND NOT EXISTS (
                SELECT 1 FROM CourseTopics child WHERE child.ParentTopicID = t.TopicID
            );
        ", connection);

        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetGapTopicCountAsync(
        int offeredCourseId,
        int programCourseId,
        List<int> comparableOfferedCourseIds)
    {
        if (comparableOfferedCourseIds.Count <= 1)
        {
            return 0;
        }

        var leafTopics = await GetLeafTopicIdsAsync(programCourseId);
        var progressMap = await GetProgressMapAsync(comparableOfferedCourseIds);
        var gapCount = 0;

        foreach (var topicId in leafTopics)
        {
            var currentKey = $"{offeredCourseId}|{topicId}";
            var currentCompleted = progressMap.TryGetValue(currentKey, out var value) && value;

            if (currentCompleted)
            {
                continue;
            }

            var coveredByOther = comparableOfferedCourseIds
                .Where(id => id != offeredCourseId)
                .Any(id => progressMap.TryGetValue($"{id}|{topicId}", out var other) && other);

            if (coveredByOther)
            {
                gapCount++;
            }
        }

        return gapCount;
    }

    private async Task<List<int>> GetLeafTopicIdsAsync(int programCourseId)
    {
        var topicIds = new List<int>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT t.TopicID
            FROM CourseTopics t
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND NOT EXISTS (
                SELECT 1 FROM CourseTopics child WHERE child.ParentTopicID = t.TopicID
            );
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            topicIds.Add(Convert.ToInt32(reader["TopicID"]));
        }

        return topicIds;
    }

    private async Task<Dictionary<string, bool>> GetProgressMapAsync(List<int> offeredCourseIds)
    {
        var map = new Dictionary<string, bool>();

        if (offeredCourseIds.Count == 0)
        {
            return map;
        }

        var ids = string.Join(",", offeredCourseIds);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand($@"
            SELECT OfferedCourseID, TopicID, IsCompleted
            FROM OfferedCourseTopicProgress
            WHERE OfferedCourseID IN ({ids});
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

    private async Task<string> GetFolderStatusLabelAsync(int offeredCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT FolderStatus
            FROM CourseFolders
            WHERE OfferedCourseID = @OfferedCourseID;
        ", connection);

        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        var result = await command.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            return "Open";
        }

        var status = Convert.ToString(result) ?? "Open";
        return status == "Submitted" ? "Submitted" : "Open";
    }

    private async Task<CourseFolderInfo?> GetCourseFolderInfoAsync(int offeredCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT CourseFolderID, OfferedCourseID, RootFolderName, RootFolderPath, FolderStatus
            FROM CourseFolders
            WHERE OfferedCourseID = @OfferedCourseID;
        ", connection);

        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new CourseFolderInfo
        {
            CourseFolderId = Convert.ToInt32(reader["CourseFolderID"]),
            OfferedCourseId = Convert.ToInt32(reader["OfferedCourseID"]),
            RootFolderName = Convert.ToString(reader["RootFolderName"]) ?? "",
            RootFolderPath = Convert.ToString(reader["RootFolderPath"]) ?? "",
            FolderStatus = Convert.ToString(reader["FolderStatus"]) ?? ""
        };
    }

    private async Task<DateTime?> GetLatestSubmissionDateAsync(int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT TOP 1 SubmittedAt
            FROM FolderSubmissions
            WHERE CourseFolderID = @CourseFolderID
            AND SubmissionStatus = 'Submitted'
            ORDER BY SubmittedAt DESC;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        var result = await command.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToDateTime(result);
    }

    private async Task<int> GetFolderProgressPercentAsync(int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                COUNT(DISTINCT d.DirectoryID) AS TotalFolders,
                COUNT(DISTINCT CASE WHEN f.FileID IS NOT NULL THEN d.DirectoryID END) AS FoldersWithFiles
            FROM CourseFolderDirectories d
            LEFT JOIN CourseFolderFiles f ON f.DirectoryID = d.DirectoryID
            WHERE d.CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return 0;
        }

        var total = Convert.ToInt32(reader["TotalFolders"]);
        var withFiles = Convert.ToInt32(reader["FoldersWithFiles"]);

        return total == 0 ? 0 : (int)Math.Round(withFiles * 100m / total);
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

    private sealed class CourseFolderInfo
    {
        public int CourseFolderId { get; set; }
        public int OfferedCourseId { get; set; }
        public string RootFolderName { get; set; } = string.Empty;
        public string RootFolderPath { get; set; } = string.Empty;
        public string FolderStatus { get; set; } = string.Empty;
    }
}

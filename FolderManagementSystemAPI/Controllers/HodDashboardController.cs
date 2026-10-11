using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

    [HttpGet("courses")]
    public async Task<ActionResult<HodDashboardCoursesPageDto>> GetCoursesPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] bool lite = false,
        [FromQuery] int? hodUserId = null)
    {
        var effectiveHodUserId = hodUserId;
        if (effectiveHodUserId is null or <= 0)
        {
            var claim =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            if (int.TryParse(claim, out var fromClaim) && fromClaim > 0)
            {
                effectiveHodUserId = fromClaim;
            }
        }

        if (effectiveHodUserId is > 0 && !await IsValidHodAsync(effectiveHodUserId.Value))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        var q = (search ?? string.Empty).Trim().ToLowerInvariant();

        var query = _context.OfferedCourses.AsNoTracking().Select(oc => new
        {
            oc.OfferedCourseId,
            oc.SectionId,
            oc.CourseId,
            oc.TeacherUserId,
            TeacherName = oc.TeacherUser.FullName,
            CourseTitle = oc.Course.CourseTitle,
            CourseCode = oc.Course.CourseCode ?? string.Empty,
            SectionName = oc.Section.SectionName,
            ProgramId = oc.Section.Session.ProgramId,
            ProgramName = oc.Section.Session.Program.ProgramName ?? string.Empty,
            ProgramCode = oc.Section.Session.Program.ProgramCode ?? string.Empty
        });

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(oc =>
                oc.TeacherName.ToLower().Contains(q) ||
                oc.SectionName.ToLower().Contains(q) ||
                oc.CourseTitle.ToLower().Contains(q) ||
                oc.CourseCode.ToLower().Contains(q) ||
                oc.ProgramName.ToLower().Contains(q) ||
                oc.ProgramCode.ToLower().Contains(q));
        }

        var totalCount = await query.CountAsync();
        var offerings = await query
            .OrderBy(oc => oc.CourseTitle)
            .ThenBy(oc => oc.SectionName)
            .ThenBy(oc => oc.TeacherName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ids = offerings.Select(o => o.OfferedCourseId).ToList();
        var folderMap = new Dictionary<int, string>();
        if (ids.Count > 0)
        {
            var folderRows = await _context.CourseFolders.AsNoTracking()
                .Where(f => ids.Contains(f.OfferedCourseId))
                .Select(f => new { f.OfferedCourseId, f.FolderStatus })
                .ToListAsync();
            foreach (var row in folderRows)
            {
                folderMap[row.OfferedCourseId] = row.FolderStatus ?? "Open";
            }
        }

        var progressMap = new Dictionary<int, (int Total, int Covered)>();
        if (!lite && ids.Count > 0)
        {
            var progressRows = await _context.OfferedCourseTopicProgresses.AsNoTracking()
                .Where(p => ids.Contains(p.OfferedCourseId))
                .GroupBy(p => p.OfferedCourseId)
                .Select(g => new
                {
                    OfferedCourseId = g.Key,
                    Total = g.Count(),
                    Covered = g.Count(x => x.IsCompleted)
                })
                .ToListAsync();
            foreach (var row in progressRows)
            {
                progressMap[row.OfferedCourseId] = (row.Total, row.Covered);
            }
        }

        var items = offerings.Select(oc =>
        {
            folderMap.TryGetValue(oc.OfferedCourseId, out var rawStatus);
            var folderStatus = NormalizeFolderStatus(rawStatus);
            var locked = folderStatus is "Submitted" or "Approved";
            progressMap.TryGetValue(oc.OfferedCourseId, out var progress);
            var total = progress.Total;
            var covered = progress.Covered;

            return new HodCourseCardDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                SectionId = oc.SectionId,
                CourseId = oc.CourseId,
                ProgramId = oc.ProgramId,
                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherName,
                CourseTitle = oc.CourseTitle,
                CourseCode = oc.CourseCode,
                SectionName = oc.SectionName,
                ProgramName = oc.ProgramName,
                ProgramCode = oc.ProgramCode,
                StatusLabel = folderStatus,
                FolderStatus = folderStatus,
                IsSectionA = IsSectionAName(oc.SectionName),
                WeeklyProgressPercentage = total == 0 ? 0 : Math.Round(covered * 100m / total, 1),
                CoveredTopics = covered,
                TotalTopics = total,
                DeviationTopics = Math.Max(total - covered, 0),
                HasWeeklyContent = total > 0,
                IsLocked = locked,
                IsFolderLocked = locked,
                FolderSubmitted = folderStatus == "Submitted",
                IsSubmitted = folderStatus == "Submitted"
            };
        }).ToList();

        return Ok(new HodDashboardCoursesPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasMore = page * pageSize < totalCount
        });
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

        // Summary only — permissions/sections load per course on expand.
        var dashboard = await _dashboardService.BuildSummaryAsync();
        return Ok(dashboard);
    }

    [HttpGet("{hodUserId:int}/courses-dashboard/{courseId:int}")]
    public async Task<ActionResult<HodCourseDashboardBlockDto>> GetCourseDashboardDetail(
        int hodUserId,
        int courseId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            return NotFound(new { message = "HOD user not found." });
        }

        var detail = await _dashboardService.BuildCourseDetailAsync(courseId);
        if (detail == null)
        {
            return NotFound(new { message = "Course not found in the current offered-courses dashboard." });
        }

        return Ok(detail);
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

        var submitted = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join cf in _context.CourseFolders.AsNoTracking() on oc.OfferedCourseId equals cf.OfferedCourseId
            where cf.FolderStatus == "Submitted" || cf.FolderStatus == "Approved"
            select new
            {
                oc.OfferedCourseId,
                oc.CourseId,
                oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode ?? string.Empty,
                SectionName = oc.Section.SectionName,
                ProgramId = oc.Section.Session.ProgramId,
                ProgramName = oc.Section.Session.Program.ProgramName ?? string.Empty,
                CourseFolderId = cf.CourseFolderId,
                FolderStatus = cf.FolderStatus ?? "Submitted"
            }).ToListAsync();

        var folderIds = submitted.Select(x => x.CourseFolderId).Distinct().ToList();
        var submittedAtMap = folderIds.Count == 0
            ? new Dictionary<int, DateTime?>()
            : await _context.FolderSubmissions.AsNoTracking()
                .Where(s => folderIds.Contains(s.CourseFolderId))
                .GroupBy(s => s.CourseFolderId)
                .Select(g => new { CourseFolderId = g.Key, SubmittedAt = g.Max(x => x.SubmittedAt) })
                .ToDictionaryAsync(x => x.CourseFolderId, x => (DateTime?)x.SubmittedAt);

        var result = submitted
            .Select(oc =>
            {
                submittedAtMap.TryGetValue(oc.CourseFolderId, out var submittedAt);
                var folderStatus = NormalizeFolderStatus(oc.FolderStatus);
                return new HodSubmittedReviewDto
                {
                    OfferedCourseId = oc.OfferedCourseId,
                    CourseId = oc.CourseId,
                    TeacherUserId = oc.TeacherUserId,
                    TeacherName = oc.TeacherName,
                    CourseTitle = oc.CourseTitle,
                    CourseCode = oc.CourseCode,
                    SectionName = oc.SectionName,
                    ProgramId = oc.ProgramId,
                    ProgramName = oc.ProgramName,
                    SubmittedAt = submittedAt,
                    OverallProgressPercent = 0,
                    FolderStatus = folderStatus,
                    ReviewStatus = folderStatus == "Approved" ? "Approved" : "Pending"
                };
            })
            .OrderBy(x => x.FolderStatus == "Approved" ? 1 : 0)
            .ThenByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
            .ThenBy(x => x.CourseTitle)
            .ToList();

        return Ok(result);
    }

    private async Task<bool> IsValidHodAsync(int hodUserId)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == hodUserId && u.Role.RoleName == "HOD");
    }

    private static string NormalizeFolderStatus(string? status)
    {
        return status switch
        {
            "Submitted" => "Submitted",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            _ => "Open"
        };
    }

    private async Task<HodCourseCardDto> BuildCourseCardAsync(OfferedCourse oc)
    {
        var programId = oc.Section.Session.ProgramId;
        var sessionId = oc.Section.SessionId;
        var programCourseId = await EnsureProgramCourseAsync(programId, oc.CourseId);

        var comparableIds = await GetComparableOfferedCourseIdsAsync(
            programId,
            oc.CourseId,
            sessionId,
            oc.OfferedCourseId);
        await EnsureProgressRowsForAllAsync(programCourseId, sessionId, comparableIds);

        var leafTopicIds = await GetLeafTopicIdsAsync(programCourseId, sessionId);
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
            SectionId = oc.SectionId,
            CourseId = oc.CourseId,
            ProgramId = programId,
            ProgramCourseId = programCourseId,
            TeacherUserId = oc.TeacherUserId,
            TeacherName = oc.TeacherUser.FullName,
            CourseTitle = oc.Course.CourseTitle,
            CourseCode = oc.Course.CourseCode,
            SectionName = oc.Section.SectionName,
            ProgramName = oc.Section.Session.Program.ProgramName,
            ProgramCode = oc.Section.Session.Program.ProgramCode ?? string.Empty,
            StatusLabel = folderStatus,
            FolderStatus = folderStatus,
            IsSectionA = IsSectionAName(oc.Section.SectionName),
            CanManageClos = permissionFlags.CanManageClos,
            CanUploadWeeklyContent = permissionFlags.CanUploadWeeklyContent,
            CanUploadMidExamQuestions = permissionFlags.CanUploadMidExamQuestions,
            CanUploadFinalExamQuestions = permissionFlags.CanUploadFinalExamQuestions,
            CanManageCloPloMapping = permissionFlags.CanManageCloPloMapping,
            CanManageCloTaskMapping = permissionFlags.CanManageCloTaskMapping,
            HasWeeklyContent = metrics.HasWeeklyContent,
            TotalTopics = metrics.TotalTopics,
            CoveredTopics = metrics.CoveredTopics,
            DeviationTopics = metrics.GapTopics,
            WeeklyProgressPercentage = metrics.ProgressPercentage,
            SyncScore = metrics.SyncScore,
            ProgressPercentage = metrics.ProgressPercentage,
            GapTopics = metrics.GapTopics,
            DeviationLabel = metrics.DeviationLabel,
            ShowActionButtons = metrics.ShowActionButtons,
            IsLocked = folderStatus is "Submitted" or "Approved",
            IsFolderLocked = folderStatus is "Submitted" or "Approved",
            FolderSubmitted = folderStatus == "Submitted",
            IsSubmitted = folderStatus == "Submitted"
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

    private async Task<List<int>> GetComparableOfferedCourseIdsAsync(
        int programId,
        int courseId,
        int sessionId,
        int currentOfferedCourseId)
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
            AND s.SessionID = @SessionID;
        ", connection);

        command.Parameters.AddWithValue("@CourseID", courseId);
        command.Parameters.AddWithValue("@ProgramID", programId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ids.Add(Convert.ToInt32(reader["OfferedCourseID"]));
        }

        return ids;
    }

    private async Task EnsureProgressRowsForAllAsync(
        int programCourseId,
        int sessionId,
        List<int> offeredCourseIds)
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
                AND t.SessionID = @SessionID
                AND NOT EXISTS (
                    SELECT 1 FROM OfferedCourseTopicProgress p
                    WHERE p.OfferedCourseID = @OfferedCourseID AND p.TopicID = t.TopicID
                );
            ", connection);

            command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
            command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
            command.Parameters.AddWithValue("@SessionID", sessionId);

            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<int>> GetLeafTopicIdsAsync(int programCourseId, int sessionId)
    {
        var ids = new List<int>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT t.TopicID
            FROM CourseTopics t
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND t.SessionID = @SessionID
            AND NOT EXISTS (
                SELECT 1
                FROM CourseTopics child
                WHERE child.ParentTopicID = t.TopicID
                AND child.SessionID = t.SessionID
            );
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ids.Add(Convert.ToInt32(reader["TopicID"]));
        }

        return ids;
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
        return status == "Submitted" ? "Submitted"
            : status == "Approved" ? "Approved"
            : status == "Rejected" ? "Rejected"
            : "Open";
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

    private static bool IsSectionAName(string? sectionName) =>
        SectionNameFormatter.IsSectionA(sectionName);

    private sealed class CourseFolderInfo
    {
        public int CourseFolderId { get; set; }
        public int OfferedCourseId { get; set; }
        public string RootFolderName { get; set; } = string.Empty;
        public string RootFolderPath { get; set; } = string.Empty;
        public string FolderStatus { get; set; } = string.Empty;
    }
}

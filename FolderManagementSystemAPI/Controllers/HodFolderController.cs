using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodFolderController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly NotificationService _notificationService;
    private readonly string _connectionString;

    public HodFolderController(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        NotificationService notificationService)
    {
        _context = context;
        _environment = environment;
        _notificationService = notificationService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    [HttpGet("submitted/{offeredCourseId:int}")]
    public async Task<ActionResult<CourseFolderOverviewDto>> GetSubmittedFolderOverview(int offeredCourseId)
    {
        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
            .Include(x => x.TeacherUser)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null)
        {
            return NotFound(new { message = "Course folder has not been created yet." });
        }

        if (!IsReviewableStatus(folder.FolderStatus))
        {
            return BadRequest(new { message = "This folder is not awaiting HOD review." });
        }

        var dto = new CourseFolderOverviewDto
        {
            OfferedCourseId = offeredCourseId,
            CourseTitle = oc.Course.CourseTitle,
            SectionName = oc.Section.SectionName,
            TeacherName = oc.TeacherUser.FullName,
            IsCreated = true,
            IsLocked = IsFolderLocked(folder.FolderStatus),
            CourseFolderId = folder.CourseFolderId,
            RootFolderName = folder.RootFolderName,
            RootFolderPath = folder.RootFolderPath,
            FolderStatus = folder.FolderStatus
        };

        dto.Folders = await GetFolderItemsAsync(folder.CourseFolderId);
        ApplyOverviewProgress(dto);

        return Ok(dto);
    }

    [HttpGet("submitted/{offeredCourseId:int}/folders/{directoryId:int}/files")]
    public async Task<ActionResult<IEnumerable<CourseFolderFileDto>>> GetSubmittedFolderFiles(
        int offeredCourseId,
        int directoryId)
    {
        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null || !IsReviewableStatus(folder.FolderStatus))
        {
            return BadRequest(new { message = "Submitted folder not found." });
        }

        var belongs = await DirectoryBelongsToFolderAsync(directoryId, folder.CourseFolderId);

        if (!belongs)
        {
            return NotFound(new { message = "Folder not found in this submission." });
        }

        return Ok(await GetFolderFilesAsync(directoryId));
    }

    [HttpPost("submitted/{offeredCourseId:int}/approve")]
    public async Task<ActionResult<ReviewFolderResultDto>> ApproveFolder(
        int offeredCourseId,
        [FromBody] ReviewFolderSubmissionDto dto)
    {
        var authError = await EnsureHodAsync(dto.HodUserId);
        if (authError != null) return authError;

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (folder == null)
        {
            return NotFound(new { message = "Course folder not found." });
        }

        if (!string.Equals(folder.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only submitted folders can be approved." });
        }

        try
        {
            await UpdateFolderStatusAsync(folder.CourseFolderId, "Approved");
            await UpdateLatestSubmissionStatusAsync(
                folder.CourseFolderId,
                "Approved",
                string.IsNullOrWhiteSpace(dto.Feedback)
                    ? "Folder approved by HOD."
                    : dto.Feedback.Trim());
        }
        catch (SqlException)
        {
            return BadRequest(new { message = "Could not approve this folder. The folder status is not allowed by the database." });
        }

        await NotifyTeacherFolderReviewAsync(
            offeredCourseId,
            "Course folder approved",
            "Your submitted course folder was approved by the HOD. It stays locked for viewing.");

        return Ok(new ReviewFolderResultDto
        {
            Message = "Folder approved. It remains locked for the teacher.",
            FolderStatus = "Approved",
            SubmissionStatus = "Approved",
            IsLocked = true
        });
    }

    [HttpPost("submitted/{offeredCourseId:int}/reject")]
    public async Task<ActionResult<ReviewFolderResultDto>> RejectFolder(
        int offeredCourseId,
        [FromBody] ReviewFolderSubmissionDto dto)
    {
        var authError = await EnsureHodAsync(dto.HodUserId);
        if (authError != null) return authError;

        if (string.IsNullOrWhiteSpace(dto.Feedback))
        {
            return BadRequest(new { message = "Feedback is required when rejecting a folder." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (folder == null)
        {
            return NotFound(new { message = "Course folder not found." });
        }

        if (!string.Equals(folder.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only submitted folders can be rejected." });
        }

        try
        {
            await MoveFolderBackToTeacherAsync(folder);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Could not return folder to teacher: {ex.Message}" });
        }

        await UpdateFolderStatusAndRootPathAsync(
            folder.CourseFolderId,
            "Rejected",
            $"src/Folders/{folder.RootFolderName}/MasterFolder");

        await UpdateLatestSubmissionStatusAsync(
            folder.CourseFolderId,
            "Rejected",
            dto.Feedback.Trim());

        await InsertSubmissionIssueAsync(folder.CourseFolderId, dto.Feedback.Trim());
        await NotifyTeacherFolderReviewAsync(
            offeredCourseId,
            "Course folder rejected",
            $"Your submitted course folder was rejected by the HOD. Reason: {dto.Feedback.Trim()}");

        return Ok(new ReviewFolderResultDto
        {
            Message = "Folder rejected and unlocked for teacher revision.",
            FolderStatus = "Rejected",
            SubmissionStatus = "Rejected",
            IsLocked = false
        });
    }

    [HttpPost("submitted/{offeredCourseId:int}/unlock")]
    public async Task<ActionResult<ReviewFolderResultDto>> UnlockFolder(
        int offeredCourseId,
        [FromBody] ReviewFolderSubmissionDto dto)
    {
        var authError = await EnsureHodAsync(dto.HodUserId);
        if (authError != null) return authError;

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (folder == null)
        {
            return NotFound(new { message = "Course folder not found." });
        }

        if (!IsFolderLocked(folder.FolderStatus))
        {
            return BadRequest(new { message = "This folder is already unlocked." });
        }

        try
        {
            await MoveFolderBackToTeacherAsync(folder);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Could not unlock folder: {ex.Message}" });
        }

        var teacherRoot = $"src/Folders/{folder.RootFolderName}/MasterFolder";
        await UpdateFolderStatusAndRootPathAsync(folder.CourseFolderId, "Generated", teacherRoot);

        var remarks = string.IsNullOrWhiteSpace(dto.Feedback)
            ? "Folder unlocked by HOD for teacher revision."
            : dto.Feedback.Trim();

        await UpdateLatestSubmissionStatusAsync(folder.CourseFolderId, "Unlocked", remarks);

        return Ok(new ReviewFolderResultDto
        {
            Message = "Folder unlocked. Teacher can edit and resubmit.",
            FolderStatus = "Generated",
            SubmissionStatus = "Unlocked",
            IsLocked = false
        });
    }

    private async Task NotifyTeacherFolderReviewAsync(int offeredCourseId, string title, string message)
    {
        var teacherUserId = await _context.OfferedCourses
            .AsNoTracking()
            .Where(oc => oc.OfferedCourseId == offeredCourseId)
            .Select(oc => oc.TeacherUserId)
            .FirstOrDefaultAsync();
        if (teacherUserId <= 0)
        {
            return;
        }

        await _notificationService.CreateAsync(
            teacherUserId,
            title,
            message,
            "CourseFolder",
            offeredCourseId);
    }

    private async Task<ActionResult?> EnsureHodAsync(int hodUserId)
    {
        if (hodUserId <= 0)
        {
            return BadRequest(new { message = "HodUserId is required." });
        }

        var user = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == hodUserId);

        if (user == null)
        {
            return NotFound(new { message = "HOD user not found." });
        }

        if (!string.Equals(user.Role?.RoleName, "HOD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(user.Role?.RoleName, "Head of Department", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only HOD users can review folders." });
        }

        return null;
    }

    private async Task MoveFolderBackToTeacherAsync(CourseFolderInfo folder)
    {
        var teacherRoot = $"src/Folders/{folder.RootFolderName}/MasterFolder";
        var currentRoot = folder.RootFolderPath;

        if (string.Equals(currentRoot, teacherRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var oldAbsolute = GetAbsolutePath(currentRoot);
        var newAbsolute = GetAbsolutePath(teacherRoot);
        var newParent = Path.GetDirectoryName(newAbsolute);

        if (string.IsNullOrWhiteSpace(newParent))
        {
            throw new InvalidOperationException("Invalid teacher folder path.");
        }

        if (!Directory.Exists(oldAbsolute))
        {
            throw new InvalidOperationException($"Physical folder missing: {currentRoot}");
        }

        Directory.CreateDirectory(newParent);

        if (Directory.Exists(newAbsolute))
        {
            Directory.Delete(newAbsolute, true);
        }

        Directory.Move(oldAbsolute, newAbsolute);

        await RewritePathsAsync(folder.CourseFolderId, currentRoot, teacherRoot);
    }

    private async Task RewritePathsAsync(int courseFolderId, string oldRootPath, string newRootPath)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var updateFolder = new SqlCommand(@"
                UPDATE CourseFolders
                SET RootFolderPath = @NewRootPath
                WHERE CourseFolderID = @CourseFolderID;
            ", connection, (SqlTransaction)transaction);

            updateFolder.Parameters.AddWithValue("@NewRootPath", newRootPath);
            updateFolder.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            await updateFolder.ExecuteNonQueryAsync();

            var updateDirectories = new SqlCommand(@"
                UPDATE CourseFolderDirectories
                SET DirectoryPath = REPLACE(DirectoryPath, @OldRootPath, @NewRootPath)
                WHERE CourseFolderID = @CourseFolderID;
            ", connection, (SqlTransaction)transaction);

            updateDirectories.Parameters.AddWithValue("@OldRootPath", oldRootPath);
            updateDirectories.Parameters.AddWithValue("@NewRootPath", newRootPath);
            updateDirectories.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            await updateDirectories.ExecuteNonQueryAsync();

            var updateFiles = new SqlCommand(@"
                UPDATE CourseFolderFiles
                SET FilePath = REPLACE(FilePath, @OldRootPath, @NewRootPath)
                WHERE DirectoryID IN
                (
                    SELECT DirectoryID
                    FROM CourseFolderDirectories
                    WHERE CourseFolderID = @CourseFolderID
                );
            ", connection, (SqlTransaction)transaction);

            updateFiles.Parameters.AddWithValue("@OldRootPath", oldRootPath);
            updateFiles.Parameters.AddWithValue("@NewRootPath", newRootPath);
            updateFiles.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            await updateFiles.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpdateFolderStatusAsync(int courseFolderId, string status)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            UPDATE CourseFolders
            SET FolderStatus = @FolderStatus
            WHERE CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@FolderStatus", status);
        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateFolderStatusAndRootPathAsync(int courseFolderId, string status, string rootPath)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            UPDATE CourseFolders
            SET FolderStatus = @FolderStatus,
                RootFolderPath = @RootFolderPath
            WHERE CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@FolderStatus", status);
        command.Parameters.AddWithValue("@RootFolderPath", rootPath);
        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateLatestSubmissionStatusAsync(int courseFolderId, string status, string remarks)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            UPDATE fs
            SET SubmissionStatus = @SubmissionStatus,
                Remarks = @Remarks
            FROM FolderSubmissions fs
            INNER JOIN (
                SELECT TOP 1 SubmissionID
                FROM FolderSubmissions
                WHERE CourseFolderID = @CourseFolderID
                ORDER BY SubmittedAt DESC, SubmissionID DESC
            ) latest ON latest.SubmissionID = fs.SubmissionID;
        ", connection);

        command.Parameters.AddWithValue("@SubmissionStatus", status);
        command.Parameters.AddWithValue("@Remarks", remarks);
        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        var updated = await command.ExecuteNonQueryAsync();
        if (updated > 0)
        {
            return;
        }

        var insert = new SqlCommand(@"
            INSERT INTO FolderSubmissions (CourseFolderID, SubmittedByUserID, SubmissionStatus, Remarks)
            SELECT CourseFolderID, CreatedByUserID, @SubmissionStatus, @Remarks
            FROM CourseFolders
            WHERE CourseFolderID = @CourseFolderID;
        ", connection);

        insert.Parameters.AddWithValue("@SubmissionStatus", status);
        insert.Parameters.AddWithValue("@Remarks", remarks);
        insert.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        await insert.ExecuteNonQueryAsync();
    }

    private async Task InsertSubmissionIssueAsync(int courseFolderId, string issueMessage)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            INSERT INTO FolderSubmissionIssues (SubmissionID, IssueMessage, IsResolved)
            SELECT TOP 1 SubmissionID, @IssueMessage, 0
            FROM FolderSubmissions
            WHERE CourseFolderID = @CourseFolderID
            ORDER BY SubmittedAt DESC, SubmissionID DESC;
        ", connection);

        command.Parameters.AddWithValue("@IssueMessage", issueMessage);
        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        await command.ExecuteNonQueryAsync();
    }

    private static bool IsFolderLocked(string? status) =>
        string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsReviewableStatus(string? status) =>
        string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    private string GetAbsolutePath(string relativePath)
        => FolderStoragePath.Resolve(_environment, relativePath);

    private async Task<bool> DirectoryBelongsToFolderAsync(int directoryId, int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseFolderDirectories
            WHERE DirectoryID = @DirectoryID AND CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@DirectoryID", directoryId);
        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private async Task<List<CourseFolderFileDto>> GetFolderFilesAsync(int directoryId)
    {
        var files = new List<CourseFolderFileDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT FileID, DirectoryID, FileName, FilePath, FileCategory, Remarks, UploadedAt
            FROM CourseFolderFiles
            WHERE DirectoryID = @DirectoryID
            ORDER BY UploadedAt DESC;
        ", connection);

        command.Parameters.AddWithValue("@DirectoryID", directoryId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            files.Add(new CourseFolderFileDto
            {
                FileId = Convert.ToInt32(reader["FileID"]),
                DirectoryId = Convert.ToInt32(reader["DirectoryID"]),
                FileName = Convert.ToString(reader["FileName"]) ?? "",
                FilePath = Convert.ToString(reader["FilePath"]) ?? "",
                FileCategory = Convert.ToString(reader["FileCategory"]) ?? "",
                Remarks = reader["Remarks"] == DBNull.Value ? null : Convert.ToString(reader["Remarks"]),
                UploadedAt = Convert.ToDateTime(reader["UploadedAt"])
            });
        }

        return files;
    }

    private async Task<List<CourseFolderItemDto>> GetFolderItemsAsync(int courseFolderId)
    {
        var items = new List<CourseFolderItemDto>();
        var categoryRows = await GetCategoryCountsAsync(courseFolderId);
        var counts = Services.FolderProgressHelper.BuildCategoryCountLookup(categoryRows);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                d.DirectoryID,
                d.TemplateItemID,
                t.TemplateCode,
                t.FolderItemName,
                t.SortOrder,
                d.DirectoryName,
                d.DirectoryPath,
                COUNT(f.FileID) AS FileCount
            FROM CourseFolderDirectories d
            INNER JOIN CourseFolderTemplateItems t
                ON t.TemplateItemID = d.TemplateItemID
            LEFT JOIN CourseFolderFiles f
                ON f.DirectoryID = d.DirectoryID
            WHERE d.CourseFolderID = @CourseFolderID
            GROUP BY
                d.DirectoryID,
                d.TemplateItemID,
                t.TemplateCode,
                t.FolderItemName,
                t.SortOrder,
                d.DirectoryName,
                d.DirectoryPath
            ORDER BY t.SortOrder;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var templateCode = Convert.ToString(reader["TemplateCode"]) ?? "";
            if (Services.FolderAutoContentService.IsHiddenSubmitTemplate(templateCode))
            {
                continue;
            }

            var progress = Services.FolderProgressHelper.BuildFolderProgress(templateCode, counts);

            items.Add(new CourseFolderItemDto
            {
                DirectoryId = Convert.ToInt32(reader["DirectoryID"]),
                TemplateItemId = Convert.ToInt32(reader["TemplateItemID"]),
                TemplateCode = templateCode,
                FolderItemName = Convert.ToString(reader["FolderItemName"]) ?? "",
                SortOrder = Convert.ToInt32(reader["SortOrder"]),
                DirectoryName = Convert.ToString(reader["DirectoryName"]) ?? "",
                DirectoryPath = Convert.ToString(reader["DirectoryPath"]) ?? "",
                FileCount = Convert.ToInt32(reader["FileCount"]),
                CanUpload = false,
                RequiredFileCount = progress.RequiredFileCount,
                UploadedFileCount = progress.UploadedFileCount,
                IsComplete = progress.IsComplete,
                ProgressSummary = progress.ProgressSummary,
                CategoryProgress = progress.CategoryProgress
            });
        }

        return items;
    }

    private static void ApplyOverviewProgress(CourseFolderOverviewDto dto)
    {
        dto.TotalRequiredFolders = dto.Folders.Count;
        dto.CompletedFolderCount = dto.Folders.Count(x => x.IsComplete);
        dto.OverallProgressPercent = dto.TotalRequiredFolders == 0
            ? 0
            : (int)Math.Round(dto.CompletedFolderCount * 100m / dto.TotalRequiredFolders);
    }

    private async Task<List<(string TemplateCode, string Category, int Count)>> GetCategoryCountsAsync(
        int courseFolderId)
    {
        var rows = new List<(string TemplateCode, string Category, int Count)>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                t.TemplateCode,
                f.FileCategory,
                COUNT(f.FileID) AS FileCount
            FROM CourseFolderDirectories d
            INNER JOIN CourseFolderTemplateItems t
                ON t.TemplateItemID = d.TemplateItemID
            LEFT JOIN CourseFolderFiles f
                ON f.DirectoryID = d.DirectoryID
            WHERE d.CourseFolderID = @CourseFolderID
            GROUP BY t.TemplateCode, f.FileCategory;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var category = reader["FileCategory"] == DBNull.Value
                ? ""
                : Convert.ToString(reader["FileCategory"]) ?? "";

            rows.Add((
                Convert.ToString(reader["TemplateCode"]) ?? "",
                category,
                Convert.ToInt32(reader["FileCount"])));
        }

        return rows;
    }

    private async Task<CourseFolderInfo?> GetCourseFolderInfoByOfferedCourseAsync(int offeredCourseId)
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

    private sealed class CourseFolderInfo
    {
        public int CourseFolderId { get; set; }
        public int OfferedCourseId { get; set; }
        public string RootFolderName { get; set; } = string.Empty;
        public string RootFolderPath { get; set; } = string.Empty;
        public string FolderStatus { get; set; } = string.Empty;
    }
}

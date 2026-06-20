using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodFolderController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly string _connectionString;

    public HodFolderController(FolderManagementDbContext context)
    {
        _context = context;
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

        if (!string.Equals(folder.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "This folder has not been submitted yet." });
        }

        var dto = new CourseFolderOverviewDto
        {
            OfferedCourseId = offeredCourseId,
            CourseTitle = oc.Course.CourseTitle,
            SectionName = oc.Section.SectionName,
            TeacherName = oc.TeacherUser.FullName,
            IsCreated = true,
            IsLocked = true,
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

        if (folder == null || !string.Equals(folder.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
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

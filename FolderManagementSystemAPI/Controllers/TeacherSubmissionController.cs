using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TeacherSubmissionController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly string _connectionString;
    private readonly TeacherPermissionService _permissionService;

    public TeacherSubmissionController(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _environment = environment;
        _permissionService = permissionService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    /* =========================================================
       1. Get all assigned courses for Submit/Folders screen
       ========================================================= */

    // Retrieves all courses assigned to a specific teacher for submission/folder view
    [HttpGet("teacher/{teacherUserId:int}/courses")]
    public async Task<ActionResult<IEnumerable<TeacherSubmissionCourseDto>>> GetTeacherSubmissionCourses(
        int teacherUserId)
    {
        var teacherExists = await _context.Users.AnyAsync(u => u.UserId == teacherUserId);

        if (!teacherExists)
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var offeredCourses = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.TeacherUserId == teacherUserId)
            .OrderBy(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ToListAsync();

        var result = new List<TeacherSubmissionCourseDto>();

        foreach (var oc in offeredCourses)
        {
            var folder = await GetCourseFolderInfoByOfferedCourseAsync(oc.OfferedCourseId);
            var isSubmitted = string.Equals(folder?.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase);

            var progressSummary = "Not started";
            var overallProgress = 0;

            if (folder != null)
            {
                var categoryRows = await GetCategoryCountsAsync(folder.CourseFolderId);
                var counts = FolderProgressHelper.BuildCategoryCountLookup(categoryRows);
                var folders = await GetFolderItemsAsync(folder.CourseFolderId, new TeacherPermissionFlagsDto(), counts);
                overallProgress = folders.Count == 0
                    ? 0
                    : (int)Math.Round(folders.Count(x => x.IsComplete) * 100m / folders.Count);
                progressSummary = isSubmitted
                    ? "Under review by HOD"
                    : $"{folders.Count(x => x.IsComplete)}/{folders.Count} folders complete";
            }

            result.Add(new TeacherSubmissionCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                CourseId = oc.CourseId,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,
                SectionName = oc.Section.SectionName,
                TeacherName = oc.TeacherUser.FullName,
                IsMasterFolderCreated = folder != null,
                FolderStatus = isSubmitted ? "Submitted" : folder?.FolderStatus,
                IsLocked = isSubmitted,
                OverallProgressPercent = overallProgress,
                ProgressSummary = progressSummary
            });
        }

        return Ok(result);
    }

    /* =========================================================
       2. Get selected course folder overview
       ========================================================= */

    // Returns full overview of a course folder including structure and metadata
    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<CourseFolderOverviewDto>> GetCourseFolderOverview(
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .Include(x => x.TeacherUser)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var effectiveTeacherUserId = teacherUserId ?? oc.TeacherUserId;
        var programCourseId = await _permissionService.EnsureProgramCourseAsync(
            oc.Section.Session.ProgramId,
            oc.CourseId);
        var permissions = await _permissionService.GetTeacherFlagsAsync(
            effectiveTeacherUserId,
            programCourseId);

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        var isSubmitted = string.Equals(folder?.FolderStatus, "Submitted", StringComparison.OrdinalIgnoreCase);

        var dto = new CourseFolderOverviewDto
        {
            OfferedCourseId = offeredCourseId,
            CourseTitle = oc.Course.CourseTitle,
            SectionName = oc.Section.SectionName,
            TeacherName = oc.TeacherUser.FullName,
            IsCreated = folder != null,
            IsLocked = isSubmitted,
            CourseFolderId = folder?.CourseFolderId,
            RootFolderName = folder?.RootFolderName,
            RootFolderPath = folder?.RootFolderPath,
            FolderStatus = isSubmitted ? "Submitted" : folder?.FolderStatus ?? "Not Created",
            Permissions = permissions
        };

        if (folder != null)
        {
            var categoryRows = await GetCategoryCountsAsync(folder.CourseFolderId);
            var counts = FolderProgressHelper.BuildCategoryCountLookup(categoryRows);
            dto.Folders = await GetFolderItemsAsync(folder.CourseFolderId, permissions, counts);

            if (isSubmitted)
            {
                foreach (var folderItem in dto.Folders)
                {
                    folderItem.CanUpload = false;
                }
            }

            dto.MissingPreview = FolderProgressHelper.BuildMissingPreview(counts)
                .Take(5)
                .ToList();
            dto.TotalRequiredFolders = dto.Folders.Count;
            dto.CompletedFolderCount = dto.Folders.Count(x => x.IsComplete);
            dto.OverallProgressPercent = dto.TotalRequiredFolders == 0
                ? 0
                : (int)Math.Round(dto.CompletedFolderCount * 100m / dto.TotalRequiredFolders);
        }

        return Ok(dto);
    }

    /* =========================================================
       3. Create MasterFolder and all a-o folders
       ========================================================= */

    // Creates master folder structure for a course if not already created
    [HttpPost("offered-course/{offeredCourseId:int}/create-master-folder")]
    public async Task<ActionResult<CourseFolderOverviewDto>> CreateMasterFolder(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
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

        if (oc.TeacherUserId != teacherUserId)
        {
            return BadRequest(new { message = "This course is not assigned to this teacher." });
        }

        var existingFolder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (existingFolder != null)
        {
            if (existingFolder.FolderStatus == "Submitted")
            {
                return BadRequest(new { message = "This folder is already submitted and locked." });
            }

            await EnsureTemplateDirectoriesAsync(existingFolder.CourseFolderId);
            return await GetCourseFolderOverview(offeredCourseId, teacherUserId);
        }

        var safeTeacher = SanitizeCompactName(oc.TeacherUser.FullName);
        var safeCourse = SanitizeCompactName(oc.Course.CourseTitle);
        var safeSection = SanitizeCompactName(oc.Section.SectionName);

        var ownerFolderName = $"{safeTeacher}{safeCourse}{safeSection}";
        var rootFolderName = "MasterFolder";
        var relativeRootPath = $"src/Folders/{ownerFolderName}/{rootFolderName}";
        var absoluteRootPath = GetAbsolutePath(relativeRootPath);

        Directory.CreateDirectory(absoluteRootPath);

        int courseFolderId;

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new SqlCommand(@"
                INSERT INTO CourseFolders
                (
                    OfferedCourseID,
                    RootFolderName,
                    RootFolderPath,
                    CreatedByUserID,
                    FolderStatus
                )
                OUTPUT INSERTED.CourseFolderID
                VALUES
                (
                    @OfferedCourseID,
                    @RootFolderName,
                    @RootFolderPath,
                    @CreatedByUserID,
                    'Generated'
                );
            ", connection);

            command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
            command.Parameters.AddWithValue("@RootFolderName", ownerFolderName);
            command.Parameters.AddWithValue("@RootFolderPath", relativeRootPath);
            command.Parameters.AddWithValue("@CreatedByUserID", teacherUserId);

            courseFolderId = Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        await EnsureTemplateDirectoriesAsync(courseFolderId);

        return await GetCourseFolderOverview(offeredCourseId, teacherUserId);
    }

    [HttpPost("offered-course/{offeredCourseId:int}/custom-folders")]
    public async Task<ActionResult<CourseFolderOverviewDto>> CreateCustomFolder(
        int offeredCourseId,
        CreateCustomFolderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FolderName))
        {
            return BadRequest(new { message = "Folder name is required." });
        }

        var oc = await _context.OfferedCourses
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (oc.TeacherUserId != dto.TeacherUserId)
        {
            return BadRequest(new { message = "This course is not assigned to this teacher." });
        }

        var programCourseId = await _permissionService.EnsureProgramCourseAsync(
            oc.Section.Session.ProgramId,
            oc.CourseId);

        var permissions = await _permissionService.GetTeacherFlagsAsync(
            dto.TeacherUserId,
            programCourseId);

        if (!HasAnyUploadPermission(permissions))
        {
            return BadRequest(new { message = "You do not have permission to create folders." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null)
        {
            return BadRequest(new { message = "Create MasterFolder before adding custom folders." });
        }

        if (folder.FolderStatus == "Submitted")
        {
            return BadRequest(new { message = "Submitted folders cannot be changed." });
        }

        var safeName = SanitizeFolderName(dto.FolderName.Trim());
        var relativeDirectoryPath = $"{folder.RootFolderPath}/{safeName}";
        var absoluteDirectoryPath = GetAbsolutePath(relativeDirectoryPath);

        Directory.CreateDirectory(absoluteDirectoryPath);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var existsCommand = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseFolderDirectories
            WHERE CourseFolderID = @CourseFolderID
            AND DirectoryName = @DirectoryName;
        ", connection);

        existsCommand.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
        existsCommand.Parameters.AddWithValue("@DirectoryName", safeName);

        if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0)
        {
            return BadRequest(new { message = "A folder with this name already exists." });
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO CourseFolderDirectories
            (CourseFolderID, ParentDirectoryID, TemplateItemID, DirectoryName, DirectoryPath)
            VALUES (@CourseFolderID, NULL, NULL, @DirectoryName, @DirectoryPath);
        ", connection);

        insertCommand.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
        insertCommand.Parameters.AddWithValue("@DirectoryName", safeName);
        insertCommand.Parameters.AddWithValue("@DirectoryPath", relativeDirectoryPath);

        await insertCommand.ExecuteNonQueryAsync();

        return await GetCourseFolderOverview(offeredCourseId, dto.TeacherUserId);
    }

    /* =========================================================
       4. Get files inside one folder
       ========================================================= */

    // Retrieves all files inside a specific directory
    [HttpGet("folders/{directoryId:int}/files")]
    public async Task<ActionResult<IEnumerable<CourseFolderFileDto>>> GetFolderFiles(
        int directoryId)
    {
        var files = new List<CourseFolderFileDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                FileID,
                DirectoryID,
                FileName,
                FilePath,
                FileCategory,
                Remarks,
                UploadedAt
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

        return Ok(files);
    }

    /* =========================================================
       5. Upload file into selected folder
       ========================================================= */

    // Uploads a file into a specific directory with validation
    [HttpPost("folders/{directoryId:int}/upload")]
    public async Task<ActionResult<FolderUploadResultDto>> UploadFileToFolder(
        int directoryId,
        [FromForm] int uploadedByUserId,
        [FromForm] string fileCategory,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a valid file." });
        }

        var directory = await GetDirectoryInfoAsync(directoryId);

        if (directory == null)
        {
            return NotFound(new { message = "Folder directory not found." });
        }

        var folderStatus = await GetFolderStatusAsync(directory.CourseFolderId);

        if (folderStatus == "Submitted")
        {
            return BadRequest(new { message = "This course folder is submitted and locked. Upload is not allowed." });
        }

        var allowedCategories = GetAllowedCategoriesForTemplateCode(directory.TemplateCode);

        if (!allowedCategories.Contains(fileCategory))
        {
            return BadRequest(new { message = "Invalid file category for this folder." });
        }

        var offeredCourseId = await GetOfferedCourseIdByDirectoryAsync(directoryId);

        if (!offeredCourseId.HasValue)
        {
            return BadRequest(new { message = "Could not resolve offered course for this folder." });
        }

        var permissions = await _permissionService.GetTeacherFlagsAsync(
            uploadedByUserId,
            await GetProgramCourseIdForOfferedCourseAsync(offeredCourseId.Value));

        if (!_permissionService.CanUploadFolderTemplate(directory.TemplateCode, permissions))
        {
            return BadRequest(new { message = "You do not have permission to upload files in this folder." });
        }

        if (!_permissionService.CanUploadFileCategory(fileCategory, permissions))
        {
            return BadRequest(new { message = "You do not have permission to upload this file type." });
        }

        var absoluteDirectoryPath = GetAbsolutePath(directory.DirectoryPath);
        Directory.CreateDirectory(absoluteDirectoryPath);

        var safeOriginalName = SanitizeFileName(file.FileName);
        var safeFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeOriginalName}";
        var relativeFilePath = $"{directory.DirectoryPath}/{safeFileName}";
        var absoluteFilePath = GetAbsolutePath(relativeFilePath);

        await using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        int fileId;

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new SqlCommand(@"
                INSERT INTO CourseFolderFiles
                (
                    DirectoryID,
                    UploadedByUserID,
                    FileName,
                    FilePath,
                    FileCategory,
                    Remarks
                )
                OUTPUT INSERTED.FileID
                VALUES
                (
                    @DirectoryID,
                    @UploadedByUserID,
                    @FileName,
                    @FilePath,
                    @FileCategory,
                    @Remarks
                );
            ", connection);

            command.Parameters.AddWithValue("@DirectoryID", directoryId);
            command.Parameters.AddWithValue("@UploadedByUserID", uploadedByUserId);
            command.Parameters.AddWithValue("@FileName", safeFileName);
            command.Parameters.AddWithValue("@FilePath", relativeFilePath);
            command.Parameters.AddWithValue("@FileCategory", fileCategory);
            command.Parameters.AddWithValue("@Remarks", DBNull.Value);

            fileId = Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        return Ok(new FolderUploadResultDto
        {
            Message = "File uploaded successfully.",
            FileId = fileId,
            FileName = safeFileName,
            FileCategory = fileCategory,
            FilePath = relativeFilePath
        });
    }

    /* =========================================================
       6. Delete file before submission
       ========================================================= */

    // Deletes a file from both disk and database if folder is not submitted
    [HttpDelete("files/{fileId:int}")]
    public async Task<IActionResult> DeleteUploadedFile(int fileId)
    {
        var file = await GetFileInfoAsync(fileId);

        if (file == null)
        {
            return NotFound(new { message = "File not found." });
        }

        var directory = await GetDirectoryInfoAsync(file.DirectoryId);

        if (directory == null)
        {
            return NotFound(new { message = "Directory not found." });
        }

        var folderStatus = await GetFolderStatusAsync(directory.CourseFolderId);

        if (folderStatus == "Submitted")
        {
            return BadRequest(new { message = "This course folder is submitted and locked. Delete is not allowed." });
        }

        var absolutePath = GetAbsolutePath(file.FilePath);

        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            DELETE FROM CourseFolderFiles
            WHERE FileID = @FileID;
        ", connection);

        command.Parameters.AddWithValue("@FileID", fileId);

        await command.ExecuteNonQueryAsync();

        return Ok(new { message = "File deleted successfully." });
    }

    /* =========================================================
       7. Validate before submission
       ========================================================= */

    // Validates whether all required files are present before submission
    [HttpPost("offered-course/{offeredCourseId:int}/validate")]
    public async Task<ActionResult<FolderValidationResultDto>> ValidateBeforeSubmit(
        int offeredCourseId)
    {
        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null)
        {
            return BadRequest(new FolderValidationResultDto
            {
                CanSubmit = false,
                MissingFiles = new List<string> { "MasterFolder has not been created yet." }
            });
        }

        var missing = await GetMissingFilesAsync(folder.CourseFolderId);

        return Ok(new FolderValidationResultDto
        {
            CanSubmit = missing.Count == 0,
            MissingFiles = missing
        });
    }

    /* =========================================================
       8. Submit folder
       ========================================================= */

    // Submits the folder, locks it, and moves it to HOD submission area
    [HttpPost("offered-course/{offeredCourseId:int}/submit")]
    public async Task<ActionResult<SubmitFolderResultDto>> SubmitFolder(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null)
        {
            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "Create MasterFolder before submitting.",
                MissingFiles = new List<string> { "MasterFolder has not been created yet." }
            });
        }

        if (folder.FolderStatus == "Submitted")
        {
            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "This folder is already submitted and locked.",
                SubmittedFolderPath = folder.RootFolderPath
            });
        }

        var missingFiles = await GetMissingFilesAsync(folder.CourseFolderId);

        if (missingFiles.Count > 0)
        {
            var failedSubmissionId = await InsertFolderSubmissionAsync(
                folder.CourseFolderId,
                teacherUserId,
                "Failed",
                "Submission failed because required files are missing.");

            await InsertSubmissionIssuesAsync(failedSubmissionId, missingFiles);

            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "Missing files. Please upload all required files before submission.",
                MissingFiles = missingFiles
            });
        }

        var oldRootPath = folder.RootFolderPath;
        var ownerFolderName = folder.RootFolderName;

        var newRootPath = $"src/HODSubmissions/{ownerFolderName}/MasterFolder";

        var oldAbsolutePath = GetAbsolutePath(oldRootPath);
        var newAbsolutePath = GetAbsolutePath(newRootPath);
        var newParent = Path.GetDirectoryName(newAbsolutePath);

        if (string.IsNullOrWhiteSpace(newParent))
        {
            return BadRequest(new { message = "Invalid submission folder path." });
        }

        if (!Directory.Exists(oldAbsolutePath))
        {
            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "Physical MasterFolder does not exist on server.",
                MissingFiles = new List<string> { $"Missing physical folder: {oldRootPath}" }
            });
        }

        Directory.CreateDirectory(newParent);

        if (Directory.Exists(newAbsolutePath))
        {
            Directory.Delete(newAbsolutePath, true);
        }

        Directory.Move(oldAbsolutePath, newAbsolutePath);

        await UpdateFolderPathsAfterSubmissionAsync(
            folder.CourseFolderId,
            oldRootPath,
            newRootPath);

        await InsertFolderSubmissionAsync(
            folder.CourseFolderId,
            teacherUserId,
            "Submitted",
            "Folder submitted successfully.");

        return Ok(new SubmitFolderResultDto
        {
            Submitted = true,
            Message = "Folder submitted successfully. It is now locked and under HOD review.",
            SubmittedFolderPath = newRootPath
        });
    }

    /* =========================================================
       9. Download a file
       ========================================================= */

    // Downloads a stored file from server
    [HttpGet("files/{fileId:int}/download")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        var file = await GetFileInfoAsync(fileId);

        if (file == null)
        {
            return NotFound(new { message = "File not found." });
        }

        var absolutePath = GetAbsolutePath(file.FilePath);

        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound(new { message = "Physical file not found on server." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(absolutePath);
        return File(bytes, "application/octet-stream", file.FileName);
    }

    /* =========================================================
       Helper methods
       ========================================================= */

    // Fetches course folder info using offered course id
    private async Task<CourseFolderInfo?> GetCourseFolderInfoByOfferedCourseAsync(int offeredCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                CourseFolderID,
                OfferedCourseID,
                RootFolderName,
                RootFolderPath,
                FolderStatus
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

    // Retrieves all folder items (a-o structure) for a course folder
    private async Task<List<CourseFolderItemDto>> GetFolderItemsAsync(
        int courseFolderId,
        TeacherPermissionFlagsDto permissions,
        Dictionary<string, int>? counts = null)
    {
        counts ??= FolderProgressHelper.BuildCategoryCountLookup(
            await GetCategoryCountsAsync(courseFolderId));

        var items = new List<CourseFolderItemDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                d.DirectoryID,
                d.TemplateItemID,
                t.TemplateCode,
                COALESCE(t.FolderItemName, d.DirectoryName) AS FolderItemName,
                t.SortOrder,
                d.DirectoryName,
                d.DirectoryPath,
                COUNT(f.FileID) AS FileCount
            FROM CourseFolderDirectories d
            LEFT JOIN CourseFolderTemplateItems t
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
            ORDER BY COALESCE(t.SortOrder, 1000), d.DirectoryName;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var templateCode = Convert.ToString(reader["TemplateCode"]) ?? "";
            var isCustom = reader["TemplateItemID"] == DBNull.Value;
            var progress = isCustom
                ? new FolderProgressHelper.FolderItemProgressResult
                {
                    RequiredFileCount = 1,
                    UploadedFileCount = Convert.ToInt32(reader["FileCount"]) > 0 ? 1 : 0,
                    IsComplete = Convert.ToInt32(reader["FileCount"]) > 0,
                    ProgressSummary = $"{Convert.ToInt32(reader["FileCount"])} file(s)"
                }
                : FolderProgressHelper.BuildFolderProgress(templateCode, counts);

            items.Add(new CourseFolderItemDto
            {
                DirectoryId = Convert.ToInt32(reader["DirectoryID"]),
                TemplateItemId = reader["TemplateItemID"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["TemplateItemID"]),
                TemplateCode = templateCode,
                FolderItemName = Convert.ToString(reader["FolderItemName"])
                    ?? Convert.ToString(reader["DirectoryName"])
                    ?? "",
                SortOrder = reader["SortOrder"] == DBNull.Value
                    ? 1000
                    : Convert.ToInt32(reader["SortOrder"]),
                DirectoryName = Convert.ToString(reader["DirectoryName"]) ?? "",
                DirectoryPath = Convert.ToString(reader["DirectoryPath"]) ?? "",
                FileCount = Convert.ToInt32(reader["FileCount"]),
                AllowedFileCategories = isCustom
                    ? new List<string> { "Other" }
                    : GetAllowedCategoriesForTemplateCode(templateCode),
                CanUpload = isCustom
                    ? HasAnyUploadPermission(permissions)
                    : _permissionService.CanUploadFolderTemplate(templateCode, permissions),
                RequiredFileCount = progress.RequiredFileCount,
                UploadedFileCount = progress.UploadedFileCount,
                IsComplete = progress.IsComplete,
                ProgressSummary = progress.ProgressSummary,
                CategoryProgress = progress.CategoryProgress,
                IsCustom = isCustom
            });
        }

        return items;
    }

    private static bool HasAnyUploadPermission(TeacherPermissionFlagsDto permissions)
    {
        return permissions.CanManageClos
            || permissions.CanUploadCourseContent
            || permissions.CanUploadExamQuestions;
    }

    private static string SanitizeFolderName(string folderName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(folderName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "CustomFolder" : cleaned;
    }

    // Ensures all template directories exist in file system and database
    private async Task EnsureTemplateDirectoriesAsync(int courseFolderId)
    {
        var folder = await GetCourseFolderInfoByIdAsync(courseFolderId);

        if (folder == null)
        {
            throw new InvalidOperationException("Course folder not found.");
        }

        var templates = await GetTemplateItemsAsync();

        foreach (var template in templates)
        {
            var directoryName = $"{template.TemplateCode}-{template.FolderItemName}";
            var relativeDirectoryPath = $"{folder.RootFolderPath}/{directoryName}";
            var absoluteDirectoryPath = GetAbsolutePath(relativeDirectoryPath);

            Directory.CreateDirectory(absoluteDirectoryPath);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var existsCommand = new SqlCommand(@"
                SELECT COUNT(1)
                FROM CourseFolderDirectories
                WHERE CourseFolderID = @CourseFolderID
                AND TemplateItemID = @TemplateItemID;
            ", connection);

            existsCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            existsCommand.Parameters.AddWithValue("@TemplateItemID", template.TemplateItemId);

            var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                continue;
            }

            var insertCommand = new SqlCommand(@"
                INSERT INTO CourseFolderDirectories
                (
                    CourseFolderID,
                    ParentDirectoryID,
                    TemplateItemID,
                    DirectoryName,
                    DirectoryPath
                )
                VALUES
                (
                    @CourseFolderID,
                    NULL,
                    @TemplateItemID,
                    @DirectoryName,
                    @DirectoryPath
                );
            ", connection);

            insertCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            insertCommand.Parameters.AddWithValue("@TemplateItemID", template.TemplateItemId);
            insertCommand.Parameters.AddWithValue("@DirectoryName", directoryName);
            insertCommand.Parameters.AddWithValue("@DirectoryPath", relativeDirectoryPath);

            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    // Fetches course folder by its ID
    private async Task<CourseFolderInfo?> GetCourseFolderInfoByIdAsync(int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                CourseFolderID,
                OfferedCourseID,
                RootFolderName,
                RootFolderPath,
                FolderStatus
            FROM CourseFolders
            WHERE CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

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

    // Retrieves all template items used for folder structure
    private async Task<List<TemplateItemInfo>> GetTemplateItemsAsync()
    {
        var items = new List<TemplateItemInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT TemplateItemID, TemplateCode, FolderItemName, SortOrder
            FROM CourseFolderTemplateItems
            ORDER BY SortOrder;
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new TemplateItemInfo
            {
                TemplateItemId = Convert.ToInt32(reader["TemplateItemID"]),
                TemplateCode = Convert.ToString(reader["TemplateCode"]) ?? "",
                FolderItemName = Convert.ToString(reader["FolderItemName"]) ?? "",
                SortOrder = Convert.ToInt32(reader["SortOrder"])
            });
        }

        return items;
    }

    // Retrieves directory metadata for a given directory
    private async Task<DirectoryInfoDto?> GetDirectoryInfoAsync(int directoryId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                d.DirectoryID,
                d.CourseFolderID,
                d.TemplateItemID,
                t.TemplateCode,
                t.FolderItemName,
                d.DirectoryName,
                d.DirectoryPath
            FROM CourseFolderDirectories d
            INNER JOIN CourseFolderTemplateItems t
                ON t.TemplateItemID = d.TemplateItemID
            WHERE d.DirectoryID = @DirectoryID;
        ", connection);

        command.Parameters.AddWithValue("@DirectoryID", directoryId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DirectoryInfoDto
        {
            DirectoryId = Convert.ToInt32(reader["DirectoryID"]),
            CourseFolderId = Convert.ToInt32(reader["CourseFolderID"]),
            TemplateItemId = Convert.ToInt32(reader["TemplateItemID"]),
            TemplateCode = Convert.ToString(reader["TemplateCode"]) ?? "",
            FolderItemName = Convert.ToString(reader["FolderItemName"]) ?? "",
            DirectoryName = Convert.ToString(reader["DirectoryName"]) ?? "",
            DirectoryPath = Convert.ToString(reader["DirectoryPath"]) ?? ""
        };
    }

    // Retrieves file metadata from database
    private async Task<FileInfoDto?> GetFileInfoAsync(int fileId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT FileID, DirectoryID, FileName, FilePath, FileCategory
            FROM CourseFolderFiles
            WHERE FileID = @FileID;
        ", connection);

        command.Parameters.AddWithValue("@FileID", fileId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new FileInfoDto
        {
            FileId = Convert.ToInt32(reader["FileID"]),
            DirectoryId = Convert.ToInt32(reader["DirectoryID"]),
            FileName = Convert.ToString(reader["FileName"]) ?? "",
            FilePath = Convert.ToString(reader["FilePath"]) ?? "",
            FileCategory = Convert.ToString(reader["FileCategory"]) ?? ""
        };
    }

    // Gets current status of course folder (Generated/Submitted/etc.)
    private async Task<string> GetFolderStatusAsync(int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT FolderStatus
            FROM CourseFolders
            WHERE CourseFolderID = @CourseFolderID;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        var result = await command.ExecuteScalarAsync();

        return result == null || result == DBNull.Value ? "" : Convert.ToString(result) ?? "";
    }

    // Validates required files and returns missing file list
    private async Task<List<string>> GetMissingFilesAsync(int courseFolderId)
    {
        var counts = FolderProgressHelper.BuildCategoryCountLookup(
            await GetCategoryCountsAsync(courseFolderId));

        return FolderProgressHelper.BuildMissingPreview(counts);
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

    // Inserts a folder submission record into database
    private async Task<int> InsertFolderSubmissionAsync(
        int courseFolderId,
        int submittedByUserId,
        string status,
        string remarks)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            INSERT INTO FolderSubmissions
            (
                CourseFolderID,
                SubmittedByUserID,
                SubmissionStatus,
                Remarks
            )
            OUTPUT INSERTED.SubmissionID
            VALUES
            (
                @CourseFolderID,
                @SubmittedByUserID,
                @SubmissionStatus,
                @Remarks
            );
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        command.Parameters.AddWithValue("@SubmittedByUserID", submittedByUserId);
        command.Parameters.AddWithValue("@SubmissionStatus", status);
        command.Parameters.AddWithValue("@Remarks", remarks);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    // Stores submission issues for failed submissions
    private async Task InsertSubmissionIssuesAsync(int submissionId, List<string> issues)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var issue in issues)
        {
            var command = new SqlCommand(@"
                INSERT INTO FolderSubmissionIssues
                (
                    SubmissionID,
                    IssueMessage,
                    IsResolved
                )
                VALUES
                (
                    @SubmissionID,
                    @IssueMessage,
                    0
                );
            ", connection);

            command.Parameters.AddWithValue("@SubmissionID", submissionId);
            command.Parameters.AddWithValue("@IssueMessage", issue);

            await command.ExecuteNonQueryAsync();
        }
    }

    // Updates folder paths after submission and locks folder
    private async Task UpdateFolderPathsAfterSubmissionAsync(
        int courseFolderId,
        string oldRootPath,
        string newRootPath)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var updateFolder = new SqlCommand(@"
                UPDATE CourseFolders
                SET
                    RootFolderPath = @NewRootPath,
                    FolderStatus = 'Submitted'
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

    // Converts relative path to absolute server path
    private string GetAbsolutePath(string relativePath)
    {
        var cleanRelative = relativePath
            .Replace("/", Path.DirectorySeparatorChar.ToString())
            .Replace("\\", Path.DirectorySeparatorChar.ToString());

        return Path.Combine(_environment.ContentRootPath, cleanRelative);
    }

    // Sanitizes file name by removing invalid characters
    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value.Trim();
    }

    // Sanitizes compact folder names for safe directory naming
    private static string SanitizeCompactName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    // Returns allowed file categories for each template section
    private static List<string> GetAllowedCategoriesForTemplateCode(string templateCode)
    {
        return templateCode switch
        {
            "a" => new List<string> { "Course Objective" },
            "b" => new List<string> { "CLO-GA Mapping" },
            "c" => new List<string> { "Course Content" },
            "d" => new List<string> { "Weekly Plan" },
            "e" => new List<string> { "Attendance Record" },
            "f" => new List<string> { "Reference Material" },

            "g" => new List<string>
            {
                "Assignment Question",
                "Quiz Question",
                "Mid Exam Question",
                "Final Exam Question"
            },

            "h" => new List<string>
            {
                "Assignment Solution",
                "Quiz Solution",
                "Mid Exam Solution",
                "Final Exam Solution"
            },

            "i" => new List<string>
            {
                "Assignment Best Sample",
                "Assignment Average Sample",
                "Assignment Worst Sample",
                "Quiz Best Sample",
                "Quiz Average Sample",
                "Quiz Worst Sample",
                "Mid Exam Best Sample",
                "Mid Exam Average Sample",
                "Mid Exam Worst Sample",
                "Final Exam Best Sample",
                "Final Exam Average Sample",
                "Final Exam Worst Sample"
            },

            "j" => new List<string> { "Marks Distribution and Grading" },
            "k" => new List<string> { "Result" },
            "l" => new List<string> { "Outcomes Assessment" },
            "m" => new List<string> { "Technology Involved" },
            "n" => new List<string> { "Design Techniques Practiced" },
            "o" => new List<string> { "Complete Analysis of Course" },

            _ => new List<string> { "Other" }
        };
    }

    private async Task<int?> GetOfferedCourseIdByDirectoryAsync(int directoryId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT cf.OfferedCourseID
            FROM CourseFolderDirectories d
            INNER JOIN CourseFolders cf ON cf.CourseFolderID = d.CourseFolderID
            WHERE d.DirectoryID = @DirectoryID;
        ", connection);

        command.Parameters.AddWithValue("@DirectoryID", directoryId);

        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
    }

    private async Task<int> GetProgramCourseIdForOfferedCourseAsync(int offeredCourseId)
    {
        var oc = await _context.OfferedCourses
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .FirstAsync(x => x.OfferedCourseId == offeredCourseId);

        return await _permissionService.EnsureProgramCourseAsync(
            oc.Section.Session.ProgramId,
            oc.CourseId);
    }

    // ===================== DATA MODELS =====================

    // Represents course folder metadata
    private class CourseFolderInfo
    {
        public int CourseFolderId { get; set; }
        public int OfferedCourseId { get; set; }
        public string RootFolderName { get; set; } = string.Empty;
        public string RootFolderPath { get; set; } = string.Empty;
        public string FolderStatus { get; set; } = string.Empty;
    }

    // Represents template structure item
    private class TemplateItemInfo
    {
        public int TemplateItemId { get; set; }
        public string TemplateCode { get; set; } = string.Empty;
        public string FolderItemName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    // Represents directory info
    private class DirectoryInfoDto
    {
        public int DirectoryId { get; set; }
        public int CourseFolderId { get; set; }
        public int TemplateItemId { get; set; }
        public string TemplateCode { get; set; } = string.Empty;
        public string FolderItemName { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
    }

    // Represents file info
    private class FileInfoDto
    {
        public int FileId { get; set; }
        public int DirectoryId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileCategory { get; set; } = string.Empty;
    }
}


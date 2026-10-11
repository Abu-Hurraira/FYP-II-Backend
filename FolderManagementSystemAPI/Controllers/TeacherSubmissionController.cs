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
    private readonly FolderDraftStorageService _draftStorage;
    private readonly FolderAutoContentService _autoContentService;
    private readonly CourseFolderProvisioningService _folderProvisioning;
    private readonly AssessmentCourseFolderLinkService _assessmentFolderLink;

    public TeacherSubmissionController(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        TeacherPermissionService permissionService,
        FolderDraftStorageService draftStorage,
        FolderAutoContentService autoContentService,
        CourseFolderProvisioningService folderProvisioning,
        AssessmentCourseFolderLinkService assessmentFolderLink)
    {
        _context = context;
        _environment = environment;
        _permissionService = permissionService;
        _draftStorage = draftStorage;
        _autoContentService = autoContentService;
        _folderProvisioning = folderProvisioning;
        _assessmentFolderLink = assessmentFolderLink;
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
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.TeacherUserId == teacherUserId)
            .OrderBy(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ToListAsync();

        var result = new List<TeacherSubmissionCourseDto>();

        foreach (var oc in offeredCourses)
        {
            var folder = await GetCourseFolderInfoByOfferedCourseAsync(oc.OfferedCourseId);
            var isLocked = IsFolderLocked(folder?.FolderStatus);

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
                progressSummary = string.Equals(folder.FolderStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                    ? "Approved by HOD"
                    : isLocked
                        ? "Under review by HOD"
                        : string.Equals(folder.FolderStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
                            ? "Rejected — revise and resubmit"
                            : $"{folders.Count(x => x.IsComplete)}/{folders.Count} folders complete";
            }
            else
            {
                var draftCounts = _draftStorage.GetCategoryCounts(oc.OfferedCourseId);
                var counts = FolderProgressHelper.BuildCategoryCountLookup(draftCounts);
                var templates = await GetTemplateItemsAsync();
                var virtualFolders = BuildVirtualFolderItems(
                    templates,
                    new TeacherPermissionFlagsDto(),
                    counts,
                    oc.OfferedCourseId);
                overallProgress = virtualFolders.Count == 0
                    ? 0
                    : (int)Math.Round(virtualFolders.Count(x => x.IsComplete) * 100m / virtualFolders.Count);
                progressSummary = virtualFolders.Count(x => x.IsComplete) == 0 && !draftCounts.Any()
                    ? "Not started"
                    : $"{virtualFolders.Count(x => x.IsComplete)}/{virtualFolders.Count} folders complete";
            }

            result.Add(new TeacherSubmissionCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                CourseId = oc.CourseId,
                ProgramId = oc.Section.Session.ProgramId,
                ProgramName = oc.Section.Session.Program?.ProgramName ?? string.Empty,
                ProgramCode = oc.Section.Session.Program?.ProgramCode ?? string.Empty,
                SessionTitle = oc.Section.Session.SessionTitle ?? string.Empty,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,
                SectionName = oc.Section.SectionName,
                TeacherName = oc.TeacherUser.FullName,
                IsMasterFolderCreated = true,
                FolderStatus = folder?.FolderStatus,
                IsLocked = isLocked,
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
        var isLocked = IsFolderLocked(folder?.FolderStatus);

        if (!isLocked)
        {
            await _autoContentService.SyncAsync(
                offeredCourseId,
                effectiveTeacherUserId,
                folder?.CourseFolderId);
            folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
            isLocked = IsFolderLocked(folder?.FolderStatus);
        }

        var dto = new CourseFolderOverviewDto
        {
            OfferedCourseId = offeredCourseId,
            ProgramCourseId = programCourseId,
            CourseTitle = oc.Course.CourseTitle,
            SectionName = oc.Section.SectionName,
            TeacherName = oc.TeacherUser.FullName,
            IsCreated = true,
            IsDraft = folder == null,
            IsLocked = isLocked,
            CourseFolderId = folder?.CourseFolderId,
            RootFolderName = folder?.RootFolderName ?? "MasterFolder",
            RootFolderPath = folder?.RootFolderPath,
            FolderStatus = folder?.FolderStatus ?? "Draft",
            ReviewStatus = folder?.FolderStatus ?? "",
            Permissions = permissions
        };

        if (folder != null)
        {
            var categoryRows = await GetCategoryCountsAsync(folder.CourseFolderId);
            var counts = FolderProgressHelper.BuildCategoryCountLookup(categoryRows);
            dto.Folders = await GetFolderItemsAsync(folder.CourseFolderId, permissions, counts);

            if (isLocked)
            {
                foreach (var folderItem in dto.Folders)
                {
                    folderItem.CanUpload = false;
                }
            }
            else
            {
                ApplyAutoManagedFolderRules(dto.Folders);
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
        else
        {
            var templates = await GetTemplateItemsAsync();
            var draftCounts = _draftStorage.GetCategoryCounts(offeredCourseId);
            var counts = FolderProgressHelper.BuildCategoryCountLookup(draftCounts);
            dto.Folders = BuildVirtualFolderItems(templates, permissions, counts, offeredCourseId);
            ApplyAutoManagedFolderRules(dto.Folders);
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
            if (IsFolderLocked(existingFolder.FolderStatus))
            {
                return BadRequest(new { message = "This folder is already submitted and locked." });
            }

            var existingRecord = await _folderProvisioning.EnsureMasterFolderAsync(
                offeredCourseId,
                teacherUserId);
            await _autoContentService.SyncAsync(
                offeredCourseId,
                teacherUserId,
                existingRecord.CourseFolderId);
            return await GetCourseFolderOverview(offeredCourseId, teacherUserId);
        }

        var createdRecord = await _folderProvisioning.EnsureMasterFolderAsync(
            offeredCourseId,
            teacherUserId);
        await _autoContentService.SyncAsync(
            offeredCourseId,
            teacherUserId,
            createdRecord.CourseFolderId);

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

        if (IsFolderLocked(folder.FolderStatus))
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
        int directoryId,
        [FromQuery] int? offeredCourseId = null)
    {
        if (directoryId < 0)
        {
            if (!offeredCourseId.HasValue)
            {
                return BadRequest(new { message = "offeredCourseId is required for draft folders." });
            }

            var templateItemId = -directoryId;
            var physicalDirectoryId = await ResolvePhysicalDirectoryIdAsync(
                offeredCourseId.Value,
                templateItemId);
            if (physicalDirectoryId.HasValue)
            {
                directoryId = physicalDirectoryId.Value;
            }
            else
            {
                return Ok(_draftStorage.ListFiles(offeredCourseId.Value, templateItemId));
            }
        }

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

        files = await PruneMissingOrJunkFilesAsync(files);

        var directory = await GetDirectoryInfoAsync(directoryId);

        if (directory != null && FolderAutoContentService.IsAutoManagedTemplate(directory.TemplateCode))
        {
            var resolvedOfferedCourseId = await GetOfferedCourseIdByDirectoryAsync(directoryId);

            if (resolvedOfferedCourseId.HasValue)
            {
                var oc = await _context.OfferedCourses
                    .FirstOrDefaultAsync(x => x.OfferedCourseId == resolvedOfferedCourseId.Value);

                if (oc != null)
                {
                    var preview = await _autoContentService.GetPreviewForTemplateAsync(
                        resolvedOfferedCourseId.Value,
                        oc.TeacherUserId,
                        directory.TemplateCode);

                    foreach (var file in files.Where(file =>
                                 string.Equals(
                                     file.Remarks,
                                     FolderAutoContentService.AutoGeneratedRemark,
                                     StringComparison.OrdinalIgnoreCase)))
                    {
                        file.ContentPreview = preview;
                    }
                }
            }
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
        [FromForm] IFormFile file,
        [FromForm] int? offeredCourseId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a valid file." });
        }

        if (directoryId < 0)
        {
            return await UploadDraftFileAsync(-directoryId, uploadedByUserId, fileCategory, file, offeredCourseId);
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

        var resolvedOfferedCourseId = await GetOfferedCourseIdByDirectoryAsync(directoryId);

        if (!resolvedOfferedCourseId.HasValue)
        {
            return BadRequest(new { message = "Could not resolve offered course for this folder." });
        }

        var permissions = await _permissionService.GetTeacherFlagsAsync(
            uploadedByUserId,
            await GetProgramCourseIdForOfferedCourseAsync(resolvedOfferedCourseId.Value));

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

        await _assessmentFolderLink.TryLinkFromFolderUploadAsync(
            resolvedOfferedCourseId.Value,
            fileId,
            safeFileName,
            fileCategory);

        return Ok(new FolderUploadResultDto
        {
            Message = "File uploaded successfully.",
            DirectoryId = directoryId,
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
    public async Task<IActionResult> DeleteUploadedFile(
        int fileId,
        [FromQuery] int? offeredCourseId = null)
    {
        if (fileId < 0)
        {
            if (!offeredCourseId.HasValue)
            {
                return BadRequest(new { message = "offeredCourseId is required for draft files." });
            }

            if (!_draftStorage.DeleteFile(offeredCourseId.Value, fileId))
            {
                var draftFile = _draftStorage.GetFile(offeredCourseId.Value, fileId);

                return NotFound(new { message = "File not found." });
            }

            return Ok(new { message = "File deleted successfully." });
        }

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

        FolderStoragePath.DeleteExistingFiles(_environment, file.FilePath);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            DELETE FROM AssessmentFiles WHERE FileID = @FileID;
            DELETE FROM CourseFolderFiles WHERE FileID = @FileID;
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
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var oc = await _context.OfferedCourses
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null || !IsFolderLocked(folder.FolderStatus))
        {
            await _autoContentService.SyncAsync(
                offeredCourseId,
                teacherUserId ?? oc.TeacherUserId,
                folder?.CourseFolderId);
        }

        if (folder == null)
        {
            var draftCounts = _draftStorage.GetCategoryCounts(offeredCourseId);
            var counts = FolderProgressHelper.BuildCategoryCountLookup(draftCounts);
            var missing = FolderProgressHelper.BuildMissingPreview(counts);

            return Ok(new FolderValidationResultDto
            {
                CanSubmit = missing.Count == 0,
                MissingFiles = missing
            });
        }

        var missingFiles = await GetMissingFilesAsync(folder.CourseFolderId);

        return Ok(new FolderValidationResultDto
        {
            CanSubmit = missingFiles.Count == 0,
            MissingFiles = missingFiles
        });
    }

    /// <summary>
    /// Publishes approved CLO and Task mapping PDF into master folder template b.
    /// </summary>
    [HttpPost("offered-course/{offeredCourseId:int}/publish-clo-task-mapping")]
    public async Task<IActionResult> PublishCloTaskMappingToFolder(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var oc = await _context.OfferedCourses
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (teacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (IsFolderLocked(folder?.FolderStatus))
        {
            return BadRequest(new { message = "Folder is locked after submission." });
        }

        try
        {
            await _autoContentService.PublishCloTaskMappingToFolderAsync(offeredCourseId, teacherUserId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "CLO-GA mapping Word file saved to the submitted folder." });
    }

    [HttpPost("offered-course/{offeredCourseId:int}/import-main-data")]
    public async Task<IActionResult> ImportMainFolderData(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        if (teacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var oc = await _context.OfferedCourses
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);
        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (IsFolderLocked(folder?.FolderStatus))
        {
            return BadRequest(new { message = "Folder is locked after submission." });
        }

        try
        {
            var imported = await _autoContentService.PublishCourseFolderDataAsync(
                offeredCourseId,
                teacherUserId);
            return Ok(new
            {
                message = "Saved main-folder data was copied into the submitted folders as PDFs.",
                importedCount = imported
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("offered-course/{offeredCourseId:int}/seed-missing-mocks")]
    public async Task<IActionResult> SeedMissingMockFiles(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        if (teacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);
        if (IsFolderLocked(folder?.FolderStatus))
        {
            return BadRequest(new { message = "Folder is locked after submission." });
        }

        try
        {
            var seeded = await _autoContentService.SeedMissingRequiredMockFilesAsync(
                offeredCourseId,
                teacherUserId);
            return Ok(new
            {
                message = "Missing folders were filled with mock files for submit testing.",
                seededCount = seeded
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("offered-course/{offeredCourseId:int}/publish-section")]
    public async Task<IActionResult> PublishSectionToFolder(
        int offeredCourseId,
        [FromQuery] int teacherUserId,
        [FromQuery] string section)
    {
        if (teacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var oc = await _context.OfferedCourses
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);
        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        try
        {
            var (_, fileName) = await _autoContentService.PublishSectionAsync(
                offeredCourseId,
                teacherUserId,
                section);
            return Ok(new
            {
                message = $"{fileName} saved to the submitted folder.",
                fileName
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
        var oc = await _context.OfferedCourses
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "Offered course not found."
            });
        }

        if (oc.TeacherUserId != teacherUserId)
        {
            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = "This course is not assigned to this teacher."
            });
        }

        var folder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId);

        if (folder == null)
        {
            var draftCounts = _draftStorage.GetCategoryCounts(offeredCourseId);
            var counts = FolderProgressHelper.BuildCategoryCountLookup(draftCounts);
            var missingFromDraft = FolderProgressHelper.BuildMissingPreview(counts);

            if (missingFromDraft.Count > 0)
            {
                return BadRequest(new SubmitFolderResultDto
                {
                    Submitted = false,
                    Message = "Missing files. Please upload all required files before submission.",
                    MissingFiles = missingFromDraft
                });
            }

            var provisioned = await _folderProvisioning.EnsureMasterFolderAsync(
                offeredCourseId,
                teacherUserId);
            folder = ToCourseFolderInfo(provisioned);
        }
        else if (!IsFolderLocked(folder.FolderStatus))
        {
            await _autoContentService.SyncAsync(offeredCourseId, teacherUserId, folder.CourseFolderId);
        }

        if (IsFolderLocked(folder.FolderStatus))
        {
            var lockedMessage = string.Equals(folder.FolderStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                ? "This folder is already approved and locked."
                : "This folder is already submitted and locked.";

            return BadRequest(new SubmitFolderResultDto
            {
                Submitted = false,
                Message = lockedMessage,
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
    public async Task<IActionResult> DownloadFile(
        int fileId,
        [FromQuery] int? offeredCourseId = null)
    {
        if (fileId < 0)
        {
            if (!offeredCourseId.HasValue)
            {
                return BadRequest(new { message = "offeredCourseId is required for draft files." });
            }

            var draftFile = _draftStorage.GetFile(offeredCourseId.Value, fileId);

            if (draftFile == null)
            {
                return NotFound(new { message = "File not found." });
            }

            var draftAbsolutePath = _draftStorage.GetAbsolutePath(draftFile.FilePath);

            if (!System.IO.File.Exists(draftAbsolutePath))
            {
                return NotFound(new { message = "Physical file not found on server." });
            }

            var draftBytes = await System.IO.File.ReadAllBytesAsync(draftAbsolutePath);
            var draftContentType = GetFileContentType(draftFile.FileName);
            return File(draftBytes, draftContentType, SanitizeFileName(draftFile.FileName));
        }

        var file = await GetFileInfoAsync(fileId);

        if (file == null)
        {
            return NotFound(new { message = "File not found." });
        }

        var absolutePath = FolderStoragePath.ResolveExistingFile(_environment, file.FilePath);

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            await DeleteOrphanFileRecordAsync(file.FileId);
            return NotFound(new { message = "Physical file not found on server." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(absolutePath);
        var contentType = GetFileContentType(file.FileName);
        return File(bytes, contentType, SanitizeFileName(file.FileName));
    }

    /* =========================================================
       Helper methods
       ========================================================= */

    private static string GetFileContentType(string? fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

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
            if (FolderAutoContentService.IsHiddenSubmitTemplate(templateCode))
            {
                continue;
            }

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
                IsCustom = isCustom,
                IsAutoManaged = FolderAutoContentService.IsAutoManagedTemplate(templateCode)
                    && !(string.Equals(templateCode, "b", StringComparison.OrdinalIgnoreCase)
                         && Convert.ToInt32(reader["FileCount"]) <= 0)
            });

        }

        return items;
    }

    private static bool HasAnyUploadPermission(TeacherPermissionFlagsDto permissions)
    {
        return permissions.CanManageClos
            || permissions.CanUploadWeeklyContent
            || permissions.CanUploadMidExamQuestions
            || permissions.CanUploadFinalExamQuestions
            || permissions.CanManageCloPloMapping
            || permissions.CanManageCloTaskMapping;
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
            SELECT FileID, DirectoryID, FileName, FilePath, FileCategory, Remarks
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
            FileCategory = Convert.ToString(reader["FileCategory"]) ?? "",
            Remarks = reader["Remarks"] == DBNull.Value ? null : Convert.ToString(reader["Remarks"])
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
        => FolderStoragePath.Resolve(_environment, relativePath);

    private async Task<List<CourseFolderFileDto>> PruneMissingOrJunkFilesAsync(List<CourseFolderFileDto> files)
    {
        var kept = new List<CourseFolderFileDto>();

        foreach (var file in files)
        {
            if (FolderAutoContentService.IsJunkAutoOrSeed(file.Remarks, file.FileName))
            {
                FolderStoragePath.DeleteExistingFiles(_environment, file.FilePath);
                await DeleteOrphanFileRecordAsync(file.FileId);
                continue;
            }

            if (FolderStoragePath.ResolveExistingFile(_environment, file.FilePath) == null)
            {
                await DeleteOrphanFileRecordAsync(file.FileId);
                continue;
            }

            kept.Add(file);
        }

        return kept;
    }

    private async Task DeleteOrphanFileRecordAsync(int fileId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var command = new SqlCommand(@"
            DELETE FROM AssessmentFiles WHERE FileID = @FileID;
            DELETE FROM CourseFolderFiles WHERE FileID = @FileID;
        ", connection);
        command.Parameters.AddWithValue("@FileID", fileId);
        await command.ExecuteNonQueryAsync();
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

    private async Task<int?> ResolvePhysicalDirectoryIdAsync(
        int offeredCourseId,
        int templateItemId)
    {
        return await (
            from directory in _context.CourseFolderDirectories.AsNoTracking()
            join folder in _context.CourseFolders.AsNoTracking()
                on directory.CourseFolderId equals folder.CourseFolderId
            where folder.OfferedCourseId == offeredCourseId
                && directory.TemplateItemId == templateItemId
            select (int?)directory.DirectoryId
        ).FirstOrDefaultAsync();
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

    private static void ApplyAutoManagedFolderRules(IEnumerable<CourseFolderItemDto> folders)
    {
        foreach (var folder in folders)
        {
            if (!FolderAutoContentService.IsAutoManagedTemplate(folder.TemplateCode))
            {
                continue;
            }

            // CLO-GA (b) stays unmarked as auto until a generated file exists.
            if (string.Equals(folder.TemplateCode, "b", StringComparison.OrdinalIgnoreCase)
                && folder.FileCount <= 0
                && folder.UploadedFileCount <= 0)
            {
                folder.IsAutoManaged = false;
                continue;
            }

            folder.IsAutoManaged = true;
        }
    }

    private List<CourseFolderItemDto> BuildVirtualFolderItems(
        List<TemplateItemInfo> templates,
        TeacherPermissionFlagsDto permissions,
        Dictionary<string, int> counts,
        int offeredCourseId)
    {
        var manifest = _draftStorage.LoadManifest(offeredCourseId);
        var fileCountByTemplate = manifest.Files
            .GroupBy(file => file.TemplateItemId)
            .ToDictionary(group => group.Key, group => group.Count());

        return templates
            .Where(template => !FolderAutoContentService.IsHiddenSubmitTemplate(template.TemplateCode))
            .Select(template =>
        {
            var directoryName = $"{template.TemplateCode}-{template.FolderItemName}";
            var progress = FolderProgressHelper.BuildFolderProgress(template.TemplateCode, counts);
            fileCountByTemplate.TryGetValue(template.TemplateItemId, out var fileCount);
            var isAutoManaged = FolderAutoContentService.IsAutoManagedTemplate(template.TemplateCode)
                && !(string.Equals(template.TemplateCode, "b", StringComparison.OrdinalIgnoreCase) && fileCount <= 0);

            return new CourseFolderItemDto
            {
                DirectoryId = -template.TemplateItemId,
                TemplateItemId = template.TemplateItemId,
                TemplateCode = template.TemplateCode,
                FolderItemName = template.FolderItemName,
                SortOrder = template.SortOrder,
                DirectoryName = directoryName,
                DirectoryPath = $"src/FolderDrafts/{offeredCourseId}/{template.TemplateItemId}",
                FileCount = fileCount,
                AllowedFileCategories = GetAllowedCategoriesForTemplateCode(template.TemplateCode),
                CanUpload = _permissionService.CanUploadFolderTemplate(template.TemplateCode, permissions),
                RequiredFileCount = progress.RequiredFileCount,
                UploadedFileCount = progress.UploadedFileCount,
                IsComplete = progress.IsComplete,
                ProgressSummary = progress.ProgressSummary,
                CategoryProgress = progress.CategoryProgress,
                IsCustom = false,
                IsAutoManaged = isAutoManaged
            };
        }).ToList();
    }

    private async Task<ActionResult<FolderUploadResultDto>> UploadDraftFileAsync(
        int templateItemId,
        int uploadedByUserId,
        string fileCategory,
        IFormFile file,
        int? offeredCourseId)
    {
        if (!offeredCourseId.HasValue)
        {
            return BadRequest(new { message = "offeredCourseId is required for draft uploads." });
        }

        var oc = await _context.OfferedCourses
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId.Value);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var existingFolder = await GetCourseFolderInfoByOfferedCourseAsync(offeredCourseId.Value);

        if (IsFolderLocked(existingFolder?.FolderStatus))
        {
            return BadRequest(new { message = "This course folder is submitted and locked. Upload is not allowed." });
        }

        var templates = await GetTemplateItemsAsync();
        var template = templates.FirstOrDefault(item => item.TemplateItemId == templateItemId);

        if (template == null)
        {
            return NotFound(new { message = "Folder template not found." });
        }

        var permissions = await _permissionService.GetTeacherFlagsAsync(
            uploadedByUserId,
            await GetProgramCourseIdForOfferedCourseAsync(offeredCourseId.Value));

        if (!_permissionService.CanUploadFolderTemplate(template.TemplateCode, permissions))
        {
            return BadRequest(new { message = "You do not have permission to upload files in this folder." });
        }

        if (!_permissionService.CanUploadFileCategory(fileCategory, permissions))
        {
            return BadRequest(new { message = "You do not have permission to upload this file type." });
        }

        var allowedCategories = GetAllowedCategoriesForTemplateCode(template.TemplateCode);

        if (!allowedCategories.Contains(fileCategory))
        {
            return BadRequest(new { message = "Invalid file category for this folder." });
        }

        var folderRecord = await _folderProvisioning.EnsureMasterFolderAsync(
            offeredCourseId.Value,
            uploadedByUserId);
        await _autoContentService.SyncAsync(
            offeredCourseId.Value,
            uploadedByUserId,
            folderRecord.CourseFolderId);

        var directory = await _folderProvisioning.GetDirectoryForTemplateItemAsync(
            folderRecord.CourseFolderId,
            templateItemId);

        if (directory == null)
        {
            return BadRequest(new { message = "Master folder directory is not ready yet." });
        }

        var (directoryId, directoryPath) = directory.Value;
        var absoluteDirectoryPath = GetAbsolutePath(directoryPath);
        Directory.CreateDirectory(absoluteDirectoryPath);

        var safeOriginalName = SanitizeFileName(file.FileName);
        var safeFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeOriginalName}";
        var relativeFilePath = $"{directoryPath}/{safeFileName}";
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

        await _assessmentFolderLink.TryLinkFromFolderUploadAsync(
            offeredCourseId.Value,
            fileId,
            safeFileName,
            fileCategory);

        return Ok(new FolderUploadResultDto
        {
            Message = "File uploaded to master folder successfully.",
            DirectoryId = directoryId,
            FileId = fileId,
            FileName = safeFileName,
            FileCategory = fileCategory,
            FilePath = relativeFilePath
        });
    }

    private static CourseFolderInfo ToCourseFolderInfo(CourseFolderRecord record) =>
        new()
        {
            CourseFolderId = record.CourseFolderId,
            OfferedCourseId = record.OfferedCourseId,
            RootFolderName = record.RootFolderName,
            RootFolderPath = record.RootFolderPath,
            FolderStatus = record.FolderStatus
        };

    // ===================== DATA MODELS =====================

    private static bool IsFolderLocked(string? status) =>
        string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

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
        public string? Remarks { get; set; }
    }
}


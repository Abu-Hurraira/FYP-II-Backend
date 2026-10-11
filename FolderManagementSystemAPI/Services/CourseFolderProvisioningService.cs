using FolderManagementSystemAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Services;

public sealed class CourseFolderRecord
{
    public int CourseFolderId { get; init; }
    public int OfferedCourseId { get; init; }
    public string RootFolderName { get; init; } = string.Empty;
    public string RootFolderPath { get; init; } = string.Empty;
    public string FolderStatus { get; init; } = string.Empty;
}

/// <summary>
/// Creates master folder structure and moves draft uploads into master paths.
/// </summary>
public class CourseFolderProvisioningService
{
    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly FolderDraftStorageService _draftStorage;
    private readonly string _connectionString;

    public CourseFolderProvisioningService(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        FolderDraftStorageService draftStorage)
    {
        _context = context;
        _environment = environment;
        _draftStorage = draftStorage;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    public async Task<CourseFolderRecord> EnsureMasterFolderAsync(
        int offeredCourseId,
        int teacherUserId,
        bool promoteDrafts = true)
    {
        var existing = await GetByOfferedCourseAsync(offeredCourseId);

        if (existing != null)
        {
            await EnsureTemplateDirectoriesAsync(existing);
            if (promoteDrafts)
            {
                await PromoteDraftFilesAsync(offeredCourseId, existing.CourseFolderId);
            }

            return existing;
        }

        var created = await CreateMasterFolderAsync(offeredCourseId, teacherUserId);
        await EnsureTemplateDirectoriesAsync(created);

        if (promoteDrafts)
        {
            await PromoteDraftFilesAsync(offeredCourseId, created.CourseFolderId);
        }

        return created;
    }

    public async Task<(int DirectoryId, string DirectoryPath)?> GetDirectoryForTemplateItemAsync(
        int courseFolderId,
        int templateItemId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT DirectoryID, DirectoryPath
            FROM CourseFolderDirectories
            WHERE CourseFolderID = @CourseFolderID
            AND TemplateItemID = @TemplateItemID;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        command.Parameters.AddWithValue("@TemplateItemID", templateItemId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (
            Convert.ToInt32(reader["DirectoryID"]),
            Convert.ToString(reader["DirectoryPath"]) ?? string.Empty);
    }

    public async Task PromoteDraftFilesAsync(int offeredCourseId, int courseFolderId)
    {
        var manifest = _draftStorage.LoadManifest(offeredCourseId);

        if (manifest.Files.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var draftFile in manifest.Files)
        {
            var dirCommand = new SqlCommand(@"
                SELECT DirectoryID, DirectoryPath
                FROM CourseFolderDirectories
                WHERE CourseFolderID = @CourseFolderID
                AND TemplateItemID = @TemplateItemID;
            ", connection);

            dirCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            dirCommand.Parameters.AddWithValue("@TemplateItemID", draftFile.TemplateItemId);

            string directoryPath;
            int directoryId;

            await using (var reader = await dirCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    continue;
                }

                directoryId = Convert.ToInt32(reader["DirectoryID"]);
                directoryPath = Convert.ToString(reader["DirectoryPath"]) ?? "";
            }

            var absoluteDraftPath = _draftStorage.GetAbsolutePath(draftFile.FilePath);
            var relativeFilePath = $"{directoryPath}/{draftFile.FileName}";
            var absoluteFilePath = GetAbsolutePath(relativeFilePath);

            Directory.CreateDirectory(GetAbsolutePath(directoryPath));

            if (!File.Exists(absoluteDraftPath))
            {
                continue;
            }

            if (File.Exists(absoluteFilePath))
            {
                File.Delete(absoluteFilePath);
            }

            File.Move(absoluteDraftPath, absoluteFilePath);

            var insertCommand = new SqlCommand(@"
                IF NOT EXISTS (
                    SELECT 1 FROM CourseFolderFiles
                    WHERE DirectoryID = @DirectoryID AND FilePath = @FilePath
                )
                BEGIN
                    INSERT INTO CourseFolderFiles
                    (
                        DirectoryID,
                        UploadedByUserID,
                        FileName,
                        FilePath,
                        FileCategory,
                        Remarks
                    )
                    VALUES
                    (
                        @DirectoryID,
                        @UploadedByUserID,
                        @FileName,
                        @FilePath,
                        @FileCategory,
                        @Remarks
                    );
                END
            ", connection);

            insertCommand.Parameters.AddWithValue("@DirectoryID", directoryId);
            insertCommand.Parameters.AddWithValue("@UploadedByUserID", draftFile.UploadedByUserId);
            insertCommand.Parameters.AddWithValue("@FileName", draftFile.FileName);
            insertCommand.Parameters.AddWithValue("@FilePath", relativeFilePath);
            insertCommand.Parameters.AddWithValue("@FileCategory", draftFile.FileCategory);
            insertCommand.Parameters.AddWithValue(
                "@Remarks",
                draftFile.IsAutoGenerated ? FolderAutoContentService.AutoGeneratedRemark : DBNull.Value);

            await insertCommand.ExecuteNonQueryAsync();
        }

        _draftStorage.Clear(offeredCourseId);
    }

    private async Task<CourseFolderRecord?> GetByOfferedCourseAsync(int offeredCourseId)
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

        return Map(reader);
    }

    private async Task<CourseFolderRecord> CreateMasterFolderAsync(int offeredCourseId, int teacherUserId)
    {
        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
            .Include(x => x.TeacherUser)
            .FirstAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc.TeacherUserId != teacherUserId)
        {
            throw new InvalidOperationException("This course is not assigned to this teacher.");
        }

        var ownerFolderName =
            $"{SanitizeCompactName(oc.TeacherUser.FullName)}" +
            $"{SanitizeCompactName(oc.Course.CourseTitle)}" +
            $"{SanitizeCompactName(oc.Section.SectionName)}";
        var relativeRootPath = $"src/Folders/{ownerFolderName}/MasterFolder";
        Directory.CreateDirectory(GetAbsolutePath(relativeRootPath));

        await using var connection = new SqlConnection(_connectionString);
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

        var courseFolderId = Convert.ToInt32(await command.ExecuteScalarAsync());

        return new CourseFolderRecord
        {
            CourseFolderId = courseFolderId,
            OfferedCourseId = offeredCourseId,
            RootFolderName = ownerFolderName,
            RootFolderPath = relativeRootPath,
            FolderStatus = "Generated"
        };
    }

    private async Task EnsureTemplateDirectoriesAsync(CourseFolderRecord folder)
    {
        var templates = await LoadTemplateItemsAsync();

        foreach (var template in templates)
        {
            var directoryName = $"{template.Code}-{template.Name}";
            var relativeDirectoryPath = $"{folder.RootFolderPath}/{directoryName}";
            Directory.CreateDirectory(GetAbsolutePath(relativeDirectoryPath));

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var existsCommand = new SqlCommand(@"
                SELECT COUNT(1)
                FROM CourseFolderDirectories
                WHERE CourseFolderID = @CourseFolderID
                AND TemplateItemID = @TemplateItemID;
            ", connection);

            existsCommand.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
            existsCommand.Parameters.AddWithValue("@TemplateItemID", template.Id);

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

            insertCommand.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
            insertCommand.Parameters.AddWithValue("@TemplateItemID", template.Id);
            insertCommand.Parameters.AddWithValue("@DirectoryName", directoryName);
            insertCommand.Parameters.AddWithValue("@DirectoryPath", relativeDirectoryPath);

            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<(int Id, string Code, string Name)>> LoadTemplateItemsAsync()
    {
        var items = new List<(int Id, string Code, string Name)>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT TemplateItemID, TemplateCode, FolderItemName
            FROM CourseFolderTemplateItems
            ORDER BY SortOrder;
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = Convert.ToString(reader["TemplateCode"]) ?? "";
            if (FolderAutoContentService.IsHiddenSubmitTemplate(code))
            {
                continue;
            }

            items.Add((
                Convert.ToInt32(reader["TemplateItemID"]),
                code,
                Convert.ToString(reader["FolderItemName"]) ?? ""));
        }

        return items;
    }

    private string GetAbsolutePath(string relativePath) =>
        FolderStoragePath.Resolve(_environment, relativePath);

    private static CourseFolderRecord Map(SqlDataReader reader) =>
        new()
        {
            CourseFolderId = Convert.ToInt32(reader["CourseFolderID"]),
            OfferedCourseId = Convert.ToInt32(reader["OfferedCourseID"]),
            RootFolderName = Convert.ToString(reader["RootFolderName"]) ?? "",
            RootFolderPath = Convert.ToString(reader["RootFolderPath"]) ?? "",
            FolderStatus = Convert.ToString(reader["FolderStatus"]) ?? ""
        };

    private static string SanitizeCompactName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}

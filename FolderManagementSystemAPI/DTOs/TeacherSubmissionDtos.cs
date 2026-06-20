namespace FolderManagementSystemAPI.DTOs;

public class FolderCategoryProgressDto
{
    public string Category { get; set; } = string.Empty;
    public int Required { get; set; }
    public int Uploaded { get; set; }
}

public class TeacherSubmissionCourseDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public bool IsMasterFolderCreated { get; set; }
    public string? FolderStatus { get; set; }
    public bool IsLocked { get; set; }
    public int OverallProgressPercent { get; set; }
    public string ProgressSummary { get; set; } = string.Empty;
}

public class CourseFolderOverviewDto
{
    public int OfferedCourseId { get; set; }
    public int? CourseFolderId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string? RootFolderName { get; set; }
    public string? RootFolderPath { get; set; }
    public string FolderStatus { get; set; } = "Not Created";
    public bool IsCreated { get; set; }
    public bool IsLocked { get; set; }
    public TeacherPermissionFlagsDto Permissions { get; set; } = new();
    public List<CourseFolderItemDto> Folders { get; set; } = new();
    public int CompletedFolderCount { get; set; }
    public int TotalRequiredFolders { get; set; }
    public int OverallProgressPercent { get; set; }
    public List<string> MissingPreview { get; set; } = new();
}

public class CourseFolderItemDto
{
    public int DirectoryId { get; set; }
    public int TemplateItemId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string FolderItemName { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int FileCount { get; set; }
    public bool CanUpload { get; set; } = true;
    public List<string> AllowedFileCategories { get; set; } = new();
    public int RequiredFileCount { get; set; }
    public int UploadedFileCount { get; set; }
    public bool IsComplete { get; set; }
    public string ProgressSummary { get; set; } = string.Empty;
    public List<FolderCategoryProgressDto> CategoryProgress { get; set; } = new();
    public bool IsCustom { get; set; }
}

public class CreateCustomFolderDto
{
    public int TeacherUserId { get; set; }
    public string FolderName { get; set; } = string.Empty;
}

public class CourseFolderFileDto
{
    public int FileId { get; set; }
    public int DirectoryId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileCategory { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class FolderUploadResultDto
{
    public string Message { get; set; } = string.Empty;
    public int FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileCategory { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class FolderValidationResultDto
{
    public bool CanSubmit { get; set; }
    public List<string> MissingFiles { get; set; } = new();
}

public class SubmitFolderResultDto
{
    public bool Submitted { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SubmittedFolderPath { get; set; }
    public List<string> MissingFiles { get; set; } = new();
}

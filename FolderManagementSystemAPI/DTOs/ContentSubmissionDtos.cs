namespace FolderManagementSystemAPI.DTOs;

public class ContentTypeStatusDto
{
    public string ContentTypeCode { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = "NotStarted";
    public int? SubmissionId { get; set; }
    public string? SubmissionStatus { get; set; }
    public string? HodFeedback { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int ItemCount { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanEdit { get; set; }
    public bool IsVisibleToAllTeachers { get; set; }
    public bool PrerequisiteMet { get; set; } = true;
    public string? BlockedReason { get; set; }
}

public class SubmitContentForReviewDto
{
    public int TeacherUserId { get; set; }
    public string ContentTypeCode { get; set; } = string.Empty;
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
}

public class SubmitContentResultDto
{
    public string Message { get; set; } = string.Empty;
    public int SubmissionId { get; set; }
    public int ItemsSubmitted { get; set; }
    public ContentTypeStatusDto Status { get; set; } = new();
}

public class HodContentReviewItemDto
{
    public int SubmissionId { get; set; }
    public int ProgramCourseId { get; set; }
    public int SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string ContentTypeCode { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public int VersionNo { get; set; }
    public int ItemCount { get; set; }
}

public class HodContentSubmissionDetailDto : HodContentReviewItemDto
{
    public List<CloDto> Clos { get; set; } = new();
    public List<WeeklyTopicDto> Weeks { get; set; } = new();
    public CloPloMatrixDto? CloPloMatrix { get; set; }
    public CloTaskMatrixDto? CloTaskMatrix { get; set; }
}

public class ReviewContentSubmissionDto
{
    public int HodUserId { get; set; }
    public string? HodFeedback { get; set; }
}

public class ReviewContentResultDto
{
    public string Message { get; set; } = string.Empty;
    public int SubmissionId { get; set; }
    public string SubmissionStatus { get; set; } = string.Empty;
}

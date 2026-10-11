namespace FolderManagementSystemAPI.DTOs;

public class HodCourseSummaryDto
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
    public int SectionCount { get; set; }
    public int TeacherCount { get; set; }
    public int OfferingCount { get; set; }
}

public class HodSubmittedReviewDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public int OverallProgressPercent { get; set; }
    public string FolderStatus { get; set; } = "Submitted";
    public string ReviewStatus { get; set; } = "Pending";
}

public class ReviewFolderSubmissionDto
{
    public int HodUserId { get; set; }
    public string? Feedback { get; set; }
}

public class ReviewFolderResultDto
{
    public string Message { get; set; } = string.Empty;
    public string FolderStatus { get; set; } = string.Empty;
    public string SubmissionStatus { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public class HodSectionProgressDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int ProgramId { get; set; }
    public int ProgramCourseId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public bool IsSectionA { get; set; }
    public bool HasWeeklyContent { get; set; }
    public int TotalTopics { get; set; }
    public int CoveredTopics { get; set; }
    public decimal SyncScore { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int GapTopics { get; set; }
    public string DeviationLabel { get; set; } = string.Empty;
    public bool ShowActionButtons { get; set; }
}

public class HodCourseDashboardBlockDto
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
    public int SectionCount { get; set; }
    public int TeacherCount { get; set; }
    /// <summary>Light names for search while details are collapsed.</summary>
    public List<string> TeacherNames { get; set; } = new();
    /// <summary>Light names for search while details are collapsed.</summary>
    public List<string> SectionNames { get; set; } = new();
    /// <summary>True when permissionGroups and sections are fully loaded.</summary>
    public bool DetailsLoaded { get; set; }
    /// <summary>Programs that have at least one offering of this course (for HOD program filter).</summary>
    public List<int> ProgramIds { get; set; } = new();
    public List<HodTeacherPermissionGroupDto> PermissionGroups { get; set; } = new();
    public List<HodSectionProgressDto> Sections { get; set; } = new();
}

public class HodCoursesDashboardDto
{
    public List<HodCourseDashboardBlockDto> Courses { get; set; } = new();
}

public class HodCourseCardDto
{
    public int OfferedCourseId { get; set; }
    public int? SectionId { get; set; }
    public int CourseId { get; set; }
    public int ProgramId { get; set; }
    public int? ProgramCourseId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = "Open";
    public string FolderStatus { get; set; } = "Open";
    public string ReviewStatus { get; set; } = string.Empty;
    public bool IsSectionA { get; set; }
    public bool CanManageClos { get; set; }
    public bool CanUploadWeeklyContent { get; set; }
    public bool CanUploadMidExamQuestions { get; set; }
    public bool CanUploadFinalExamQuestions { get; set; }
    public bool CanManageCloPloMapping { get; set; }
    public bool CanManageCloTaskMapping { get; set; }

    public bool CanUploadCourseContent
    {
        get => CanUploadWeeklyContent;
        set => CanUploadWeeklyContent = value;
    }

    public bool CanUploadExamQuestions
    {
        get => CanUploadMidExamQuestions || CanUploadFinalExamQuestions;
        set
        {
            CanUploadMidExamQuestions = value;
            CanUploadFinalExamQuestions = value;
        }
    }
    public bool HasWeeklyContent { get; set; }
    public int TotalTopics { get; set; }
    public int CoveredTopics { get; set; }
    public int DeviationTopics { get; set; }
    public decimal WeeklyProgressPercentage { get; set; }
    public decimal SyncScore { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int GapTopics { get; set; }
    public string DeviationLabel { get; set; } = string.Empty;
    public bool ShowActionButtons { get; set; }
    public bool IsLocked { get; set; }
    public bool IsFolderLocked { get; set; }
    public bool FolderSubmitted { get; set; }
    public bool IsSubmitted { get; set; }
}

public class HodDashboardCoursesPageDto
{
    public List<HodCourseCardDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}

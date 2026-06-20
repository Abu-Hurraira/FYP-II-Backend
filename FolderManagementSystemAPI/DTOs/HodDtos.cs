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
    public string ProgramName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public int OverallProgressPercent { get; set; }
}

public class HodSectionProgressDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int ProgramId { get; set; }
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
    public int CourseId { get; set; }
    public int ProgramId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = "Open";
    public bool IsSectionA { get; set; }
    public bool CanManageClos { get; set; }
    public bool CanUploadCourseContent { get; set; }
    public bool CanUploadExamQuestions { get; set; }
    public bool HasWeeklyContent { get; set; }
    public int TotalTopics { get; set; }
    public int CoveredTopics { get; set; }
    public decimal SyncScore { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int GapTopics { get; set; }
    public string DeviationLabel { get; set; } = string.Empty;
    public bool ShowActionButtons { get; set; }
}

namespace FolderManagementSystemAPI.DTOs;

public class TeacherCourseCardDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int ProgramCourseId { get; set; }
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public bool HasWeeklyContent { get; set; }
    public decimal? WeeklyProgressPercentage { get; set; }
}

public class TeacherCourseMenuDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int ProgramCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public TeacherPermissionFlagsDto Permissions { get; set; } = new();
}

public class WeeklyContentDto
{
    public int OfferedCourseId { get; set; }
    public int ProgramCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public bool HasWeeklyContent { get; set; }
    public decimal? ProgressPercentage { get; set; }
    public TeacherPermissionFlagsDto Permissions { get; set; } = new();
    public List<ContentTypeStatusDto> ContentStatuses { get; set; } = new();
    public List<WeeklyTopicDto> Weeks { get; set; } = new();
}

public class WeeklyTopicDto
{
    public int TopicId { get; set; }
    public int ProgramCourseId { get; set; }
    public int? ParentTopicId { get; set; }
    public int? WeekNo { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public string? TopicDescription { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsCompleted { get; set; }
    public List<WeeklyTopicDto> Children { get; set; } = new();
    public List<string> CloCodes { get; set; } = new();
}

public class AddWeeklyTopicDto
{
    public int TeacherUserId { get; set; }
    public int WeekNo { get; set; }
    public int? ParentTopicId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public string? TopicDescription { get; set; }
    public int? DisplayOrder { get; set; }
    public List<string>? CloCodes { get; set; }
}

public class UpdateTopicProgressDto
{
    public int TeacherUserId { get; set; }
    public bool IsCompleted { get; set; }
}

public class UploadWeeklyContentResultDto
{
    public string Message { get; set; } = string.Empty;
    public int FileId { get; set; }
    public int CreatedTopics { get; set; }
    public int ExistingTopics { get; set; }
    public int MappedClos { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class WeeklyTopicLoadSchemeDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public string? AcademicYear { get; set; }
    public DateTime? StartDate { get; set; }
    public int TopicCount { get; set; }
    public int WeekCount { get; set; }
}

public class WeeklyTopicLoadItemDto
{
    public int TopicId { get; set; }
    public int? WeekNo { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public string? TopicDescription { get; set; }
    public int ChildCount { get; set; }
    public List<string> CloCodes { get; set; } = new();
    public bool AlreadyInCurrent { get; set; }
}

public class WeeklyTopicLoadSchemeDetailDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public List<WeeklyTopicLoadItemDto> Items { get; set; } = new();
}

public class ApplyWeeklyTopicsFromSchemeDto
{
    public int TeacherUserId { get; set; }
    public int SourceSessionId { get; set; }
    public List<int> TopicIds { get; set; } = new();
}

public class ApplyWeeklyTopicsFromSchemeResultDto
{
    public int CreatedCount { get; set; }
    public int AlreadyExistCount { get; set; }
    public int MappedCloCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

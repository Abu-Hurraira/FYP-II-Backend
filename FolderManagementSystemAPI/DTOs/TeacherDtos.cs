namespace FolderManagementSystemAPI.DTOs;

public class TeacherCourseCardDto
{
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public int ProgramCourseId { get; set; }
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

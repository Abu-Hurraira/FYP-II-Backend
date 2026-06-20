namespace FolderManagementSystemAPI.DTOs;

public class TeacherSynchronizationDto
{
    public int CurrentOfferedCourseId { get; set; }
    public int ProgramCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CurrentTeacherName { get; set; } = string.Empty;
    public string CurrentSectionName { get; set; } = string.Empty;
    public List<SyncTeacherColumnDto> Teachers { get; set; } = new();
    public List<SyncMatrixRowDto> Matrix { get; set; } = new();
    public List<SyncCommonTopicDto> CommonTopics { get; set; } = new();
    public List<SyncGapTopicDto> GapTopics { get; set; } = new();
}

public class SyncTeacherColumnDto
{
    public int OfferedCourseId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
}

public class SyncMatrixRowDto
{
    public int TopicId { get; set; }
    public int? WeekNo { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public List<SyncTopicStatusDto> Statuses { get; set; } = new();
}

public class SyncTopicStatusDto
{
    public int OfferedCourseId { get; set; }
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class SyncCommonTopicDto
{
    public int TopicId { get; set; }
    public int? WeekNo { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = "COMMON";
}

public class SyncGapTopicDto
{
    public int TopicId { get; set; }
    public int? WeekNo { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string MissingInTeacher { get; set; } = string.Empty;
    public string MissingInSection { get; set; } = string.Empty;
    public int CoveredByCount { get; set; }
    public string GapLevel { get; set; } = string.Empty;
    public List<string> CoveredByTeachers { get; set; } = new();
}

namespace FolderManagementSystemAPI.DTOs;

public class GraderTaskCatalogItemDto
{
    public string TaskCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class GraderTaskProgressDto
{
    public string TaskCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Assigned { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public int Done { get; set; }
    public int Total { get; set; }
}

public class GraderAssignedCourseDto
{
    public int GraderAllocationId { get; set; }
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherEmail { get; set; } = string.Empty;
    public List<GraderTaskProgressDto> Tasks { get; set; } = new();
}

public class TeacherGraderWorkspaceDto
{
    public int GraderAllocationId { get; set; }
    public int GraderUserId { get; set; }
    public string GraderName { get; set; } = string.Empty;
    public string GraderEmail { get; set; } = string.Empty;
    public List<GraderAssignedCourseDto> Courses { get; set; } = new();
}

public class TeacherGraderTasksPageDto
{
    public List<GraderTaskCatalogItemDto> Catalog { get; set; } = new();
    public List<TeacherGraderWorkspaceDto> Graders { get; set; } = new();
}

public class SaveGraderTaskItemDto
{
    public string TaskCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class SaveGraderTasksDto
{
    public int TeacherUserId { get; set; }
    public int GraderAllocationId { get; set; }
    public int OfferedCourseId { get; set; }
    public List<string> TaskCodes { get; set; } = new();
    public List<SaveGraderTaskItemDto> Tasks { get; set; } = new();
}

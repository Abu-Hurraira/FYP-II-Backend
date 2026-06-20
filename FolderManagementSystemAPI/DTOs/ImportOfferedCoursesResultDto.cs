namespace FolderManagementSystemAPI.DTOs;

public class ImportOfferedCoursesResultDto
{
    public string Message { get; set; } = string.Empty;

    public int CreatedSections { get; set; }
    public int CreatedOfferedCourses { get; set; }
    public int UpdatedOfferedCourses { get; set; }

    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }

    public List<string> Errors { get; set; } = new();
}

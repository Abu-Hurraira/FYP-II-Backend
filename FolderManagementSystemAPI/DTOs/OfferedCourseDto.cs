public class OfferedCourseDto
{
    public int OfferedCourseId { get; set; }

    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;

    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;

    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherEmail { get; set; } = string.Empty;
}

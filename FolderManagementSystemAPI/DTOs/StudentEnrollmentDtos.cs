namespace FolderManagementSystemAPI.DTOs;

public class StudentEnrollmentLinkDto
{
    public int StudentOfferedCourseId { get; set; }
    public int OfferedCourseId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
}

public class StudentEnrollmentRowDto
{
    public int StudentId { get; set; }
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<StudentEnrollmentLinkDto> Enrollments { get; set; } = new();
}

public class EnrollSingleStudentDto
{
    public int SectionId { get; set; }
    public int OfferedCourseId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class AddStudentEnrollmentDto
{
    public int OfferedCourseId { get; set; }
}

public class ReopenAssessmentSetupDto
{
    public int TeacherUserId { get; set; }
}

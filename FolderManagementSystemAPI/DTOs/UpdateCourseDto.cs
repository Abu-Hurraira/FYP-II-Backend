namespace FolderManagementSystemAPI.DTOs
{
    public class UpdateCourseDto
    {
        public string CourseTitle { get; set; } = null!;

        public string CourseCode { get; set; } = null!;

        public int? ClassCreditHours { get; set; }

        public int? LabCreditHours { get; set; }
    }
}
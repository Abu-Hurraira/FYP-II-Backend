namespace FolderManagementSystemAPI.DTOs
{
    public class CreateOfferedCourseDto
    {
        public int SectionId { get; set; }

        public int CourseId { get; set; }

        public int TeacherUserId { get; set; }
    }
}
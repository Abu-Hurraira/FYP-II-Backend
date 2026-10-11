namespace FolderManagementSystemAPI.DTOs
{
    public class CreateOfferedCourseDto
    {
        public int SectionId { get; set; }

        public int CourseId { get; set; }

        public int TeacherUserId { get; set; }

        /// <summary>
        /// When true (default), enroll every student already in the section into this new offering.
        /// </summary>
        public bool EnrollExistingSectionStudents { get; set; } = true;
    }
}
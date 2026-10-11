namespace FolderManagementSystemAPI.DTOs
{
    public class CreateStudentDto
    {
        public int SectionId { get; set; }

        public string FullName { get; set; } = null!;

        public string AridNo { get; set; } = null!;

        public string? Email { get; set; }

        /// <summary>Optional: enroll only in this offered course instead of all section offerings.</summary>
        public int? OfferedCourseId { get; set; }
    }
}
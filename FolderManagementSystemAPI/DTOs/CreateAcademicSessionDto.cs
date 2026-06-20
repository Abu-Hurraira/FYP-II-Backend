namespace FolderManagementSystemAPI.DTOs
{
    public class CreateAcademicSessionDto
    {
        public int ProgramId { get; set; }

        public string SessionTitle { get; set; } = null!;

        public string? AcademicYear { get; set; }
    }
}
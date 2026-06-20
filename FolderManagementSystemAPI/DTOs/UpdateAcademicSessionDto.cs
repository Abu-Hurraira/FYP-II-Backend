namespace FolderManagementSystemAPI.DTOs
{
    public class UpdateAcademicSessionDto
    {
        public int ProgramId { get; set; }

        public string SessionTitle { get; set; } = null!;

        public string? AcademicYear { get; set; }
    }
}
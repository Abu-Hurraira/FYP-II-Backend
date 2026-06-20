namespace FolderManagementSystemAPI.DTOs
{
    public class CreatePloDto
    {
        public int ProgramId { get; set; }

        public string PloCode { get; set; } = null!;

        public string PloDescription { get; set; } = null!;
    }
}
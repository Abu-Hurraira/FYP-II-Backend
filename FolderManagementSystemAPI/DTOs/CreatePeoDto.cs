namespace FolderManagementSystemAPI.DTOs
{
    public class CreatePeoDto
    {
        public int ProgramId { get; set; }

        public string PeoCode { get; set; } = null!;

        public string PeoDescription { get; set; } = null!;
    }
}
namespace FolderManagementSystemAPI.DTOs
{
    public class CreateSectionDto
    {
        public int SessionId { get; set; }

        public string SectionName { get; set; } = null!;

        public int? Semester { get; set; }

        public string? StudentListFilePath { get; set; }

        public string? AttendanceSheetPath { get; set; }
    }
}
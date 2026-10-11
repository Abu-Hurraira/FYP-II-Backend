namespace FolderManagementSystemAPI.DTOs
{
    public class CreateAcademicSessionDto
    {
        public int ProgramId { get; set; }

        /// <summary>Fall, Spring, or Summer. Preferred over free-text SessionTitle.</summary>
        public string? SessionSeason { get; set; }

        /// <summary>Session start date. Year is used with season to build title (e.g. Spring 2026).</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Legacy free-text title. Used only when SessionSeason is not provided.</summary>
        public string? SessionTitle { get; set; }

        public string? AcademicYear { get; set; }
    }
}

namespace FolderManagementSystemAPI.DTOs;

public class CloDto
{
    public int CloId { get; set; }
    public int ProgramCourseId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = string.Empty;
}

public class CreateCloDto
{
    public int TeacherUserId { get; set; }
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
    public string? CloCode { get; set; }
    public string CloDescription { get; set; } = string.Empty;
}

public class UpdateCloDto
{
    public int TeacherUserId { get; set; }
    public string? CloCode { get; set; }
    public string CloDescription { get; set; } = string.Empty;
    public int? OfferedCourseId { get; set; }
}

public class CloImportResultDto
{
    public string Message { get; set; } = string.Empty;
    public int Inserted { get; set; }
    public int Skipped { get; set; }
    public List<CloDto> Clos { get; set; } = new();
}

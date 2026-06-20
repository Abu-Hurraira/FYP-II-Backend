namespace FolderManagementSystemAPI.DTOs;

public class CloDto
{
    public int CloId { get; set; }
    public int ProgramCourseId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
}

public class CreateCloDto
{
    public int TeacherUserId { get; set; }
    public string? CloCode { get; set; }
    public string CloDescription { get; set; } = string.Empty;
}

public class CloImportResultDto
{
    public string Message { get; set; } = string.Empty;
    public int Inserted { get; set; }
    public int Skipped { get; set; }
    public List<CloDto> Clos { get; set; } = new();
}

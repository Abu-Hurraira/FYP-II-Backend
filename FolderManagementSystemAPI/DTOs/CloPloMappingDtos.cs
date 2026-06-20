namespace FolderManagementSystemAPI.DTOs;

public class CloPloMatrixColumnDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
}

public class CloPloMatrixCellDto
{
    public int PloId { get; set; }
    public int? MappingId { get; set; }
    public decimal? Percentage { get; set; }
}

public class CloPloMatrixRowDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string RowLabel { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public List<CloPloMatrixCellDto> Cells { get; set; } = new();
}

public class PloRowCapacityDto
{
    public int PloId { get; set; }
    public decimal UsedPercentage { get; set; }
    public decimal RemainingPercentage { get; set; }
}

public class CloPloMatrixDto
{
    public int ProgramCourseId { get; set; }
    public int ProgramId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public List<CloPloMatrixColumnDto> Plos { get; set; } = new();
    public List<CloPloMatrixRowDto> Clos { get; set; } = new();
    public List<PloRowCapacityDto> PloRowCapacities { get; set; } = new();
}

public class SaveCloPloSingleCellDto
{
    public int UpdatedByUserId { get; set; }
    public int CloId { get; set; }
    public int PloId { get; set; }
    public decimal? Percentage { get; set; }
}

namespace FolderManagementSystemAPI.DTOs;

public class ProgramCourseMappingListItemDto
{
    public int ProgramCourseId { get; set; }
    public int ProgramId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int MappedPloCount { get; set; }
    public int TotalPloCount { get; set; }
    public decimal TotalPercentage { get; set; }
    public bool IsComplete { get; set; }
}

public class AddProgramCourseDto
{
    public int CourseId { get; set; }
}

public class ProgramCoursePloMappingItemDto
{
    public int? MappingId { get; set; }
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
    public decimal? Percentage { get; set; }
    public bool IsMapped { get; set; }
}

public class ProgramCoursePloMappingGridDto
{
    public int ProgramCourseId { get; set; }
    public int ProgramId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public int TotalPloCount { get; set; }
    public decimal TotalPercentage { get; set; }
    public List<ProgramCoursePloMappingItemDto> Plos { get; set; } = new();
}

public class SaveProgramCoursePloMappingItemDto
{
    public int PloId { get; set; }
    public decimal? Percentage { get; set; }
}

public class SaveProgramCoursePloMappingsDto
{
    public int MappedByUserId { get; set; }
    public List<SaveProgramCoursePloMappingItemDto> Mappings { get; set; } = new();
}

public class ProgramPloMatrixColumnDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
}

public class ProgramPloMatrixCellDto
{
    public int PloId { get; set; }
    public int? MappingId { get; set; }
    public decimal? Percentage { get; set; }
}

public class ProgramPloMatrixRowDto
{
    public int ProgramCourseId { get; set; }
    public int CourseId { get; set; }
    public string ShortLabel { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
    public List<ProgramPloMatrixCellDto> Cells { get; set; } = new();
}

public class ProgramPloMatrixDto
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public List<ProgramPloMatrixColumnDto> Plos { get; set; } = new();
    public List<ProgramPloMatrixRowDto> Courses { get; set; } = new();
    public List<PloColumnCapacityDto> PloColumnCapacities { get; set; } = new();
}

public class SaveProgramPloMatrixCellDto
{
    public int ProgramCourseId { get; set; }
    public int PloId { get; set; }
    public decimal? Percentage { get; set; }
}

public class SaveProgramPloMatrixDto
{
    public int MappedByUserId { get; set; }
    public List<SaveProgramPloMatrixCellDto> Cells { get; set; } = new();
}

public class PloColumnCapacityDto
{
    public int PloId { get; set; }
    public decimal UsedPercentage { get; set; }
    public decimal RemainingPercentage { get; set; }
}

public class SaveProgramPloSingleCellDto
{
    public int MappedByUserId { get; set; }
    public int ProgramCourseId { get; set; }
    public int PloId { get; set; }
    public decimal? Percentage { get; set; }
}

namespace FolderManagementSystemAPI.DTOs;

public class CloTaskMatrixColumnDto
{
    public int AssessmentTypeId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
    /// <summary>True when this task was added for the course/session (not Assignment/Quiz/Mid/Final).</summary>
    public bool IsCustom { get; set; }
}

public class CloTaskMatrixCellDto
{
    public int AssessmentTypeId { get; set; }
    public int? MappingId { get; set; }
    public decimal? Percentage { get; set; }
}

public class CloTaskMatrixRowDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string RowLabel { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public List<CloTaskMatrixCellDto> Cells { get; set; } = new();
}

public class CloRowCapacityDto
{
    public int CloId { get; set; }
    public decimal UsedPercentage { get; set; }
    public decimal RemainingPercentage { get; set; }
}

public class CloTaskMatrixDto
{
    public int ProgramCourseId { get; set; }
    public int ProgramId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public List<CloTaskMatrixColumnDto> AssessmentTypes { get; set; } = new();
    public List<CloTaskMatrixRowDto> Clos { get; set; } = new();
    public List<CloRowCapacityDto> CloRowCapacities { get; set; } = new();
}

public class SaveCloTaskSingleCellDto
{
    public int UpdatedByUserId { get; set; }
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
    public int CloId { get; set; }
    public int AssessmentTypeId { get; set; }
    public decimal? Percentage { get; set; }
}

public class AddCloTaskTypeDto
{
    public int TeacherUserId { get; set; }
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
    public string TaskName { get; set; } = string.Empty;
}

public class RemoveCloTaskTypeDto
{
    public int TeacherUserId { get; set; }
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
    public int AssessmentTypeId { get; set; }
}

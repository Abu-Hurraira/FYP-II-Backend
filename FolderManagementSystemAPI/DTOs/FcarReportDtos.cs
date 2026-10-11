namespace FolderManagementSystemAPI.DTOs;

public class FcarPloDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
}

public class FcarCloPloMappingRowDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public List<string> MappedPloCodes { get; set; } = new();
}

public class FcarGradeDistributionDto
{
    public int GradeACount { get; set; }
    public int GradeBCount { get; set; }
    public int GradeCCount { get; set; }
    public int GradeDCount { get; set; }
    public int TotalPassing { get; set; }
    public int GradeFCount { get; set; }
}

public class FcarCloItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public int GradeACount { get; set; }
    public int GradeBCount { get; set; }
    public int GradeCCount { get; set; }
    public int GradeDCount { get; set; }
    public string EemuVector { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class FcarCloAssessmentDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public List<FcarCloItemDto> Items { get; set; } = new();
    public decimal OverallCloAverage { get; set; }
}

public class FcarPloEpanItemDto
{
    public string PloCode { get; set; } = string.Empty;
    public decimal EpanAverage { get; set; }
    public decimal MetricGoal { get; set; } = 2.0m;
    public string Status { get; set; } = "PROFICIENT";
}

public class SaveFcarReportInputDto
{
    public int TeacherUserId { get; set; }
    public string CatalogDescription { get; set; } = string.Empty;
    public string CourseModifications { get; set; } = string.Empty;
    public string? StudentFeedbackText { get; set; }
    public string? ReflectionText { get; set; }
    public string? ProposedActionsText { get; set; }
}

public class FcarReportDto
{
    public int OfferedCourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public int TotalCreditHours { get; set; }
    public string CatalogDescription { get; set; } = string.Empty;

    public List<FcarPloDto> ProgramOutcomes { get; set; } = new();
    public List<FcarCloPloMappingRowDto> CloPloMappings { get; set; } = new();
    public List<string> MappedPloCodes { get; set; } = new();
    public decimal DefaultMetricGoal { get; set; } = 2.0m;

    public FcarGradeDistributionDto GradeDistribution { get; set; } = new();
    public string CourseModifications { get; set; } = "No modification";

    public List<FcarCloAssessmentDto> CloAssessments { get; set; } = new();
    public List<FcarPloEpanItemDto> PloEpans { get; set; } = new();

    public string ProgramOutcomeAssessmentText { get; set; } = string.Empty;
    public string StudentFeedbackText { get; set; } = string.Empty;
    public string ReflectionText { get; set; } = string.Empty;
    public string ProposedActionsText { get; set; } = "None";
    public List<string> CalculationWarnings { get; set; } = new();
}

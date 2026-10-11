namespace FolderManagementSystemAPI.DTOs;

public class CloAttainmentStudentDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
}

public class MarksSheetQuestionDto
{
    public int QuestionId { get; set; }
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal MaxMarks { get; set; }
    public List<string> CloCodes { get; set; } = new();
}

public class MarksSheetStudentRowDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public Dictionary<int, decimal?> MarksByQuestionId { get; set; } = new();
    /// <summary>Sum of obtained marks across gradeable questions on the paper scale (null if none entered).</summary>
    public decimal? TotalObtained { get; set; }
    /// <summary>Paper marks converted into assessment TotalMarks weight (null if none entered).</summary>
    public decimal? ConvertedObtained { get; set; }
    /// <summary>Percentage of paper performance (= converted / TotalMarks when TotalMarks set).</summary>
    public decimal? Percentage { get; set; }
}

public class AssessmentMarksSheetDto
{
    public int AssessmentId { get; set; }
    public string AssessmentTitle { get; set; } = string.Empty;
    public string AssessmentTypeName { get; set; } = string.Empty;
    public int? AssessmentNo { get; set; }
    /// <summary>Official assessment weight in the course (e.g. Assignment = 50).</summary>
    public decimal? TotalMarks { get; set; }
    /// <summary>Sum of gradeable question/part max marks on the paper scale.</summary>
    public decimal PaperMaxMarks { get; set; }
    public bool IsSetupComplete { get; set; }
    public List<MarksSheetQuestionDto> Questions { get; set; } = new();
    public List<MarksSheetStudentRowDto> Students { get; set; } = new();
}

public class SaveAssessmentMarksDto
{
    public int TeacherUserId { get; set; }
    public List<SaveStudentQuestionMarkDto> Marks { get; set; } = new();
}

public class SaveStudentQuestionMarkDto
{
    public int StudentId { get; set; }
    public int QuestionId { get; set; }
    public decimal? ObtainedMarks { get; set; }
}

public class CloAssessmentDesignColumnDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
}

public class CloAssessmentDesignRowDto
{
    public int AssessmentTypeId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public Dictionary<int, decimal?> PercentagesByCloId { get; set; } = new();
}

public class CloAssessmentDesignDto
{
    public int ProgramCourseId { get; set; }
    public int SessionId { get; set; }
    public List<CloAssessmentDesignColumnDto> Clos { get; set; } = new();
    public List<CloAssessmentDesignRowDto> Rows { get; set; } = new();
    public Dictionary<int, decimal> CloColumnTotals { get; set; } = new();
}

public class SaveCloAssessmentDesignCellDto
{
    public int TeacherUserId { get; set; }
    public int? SessionId { get; set; }
    public int? OfferedCourseId { get; set; }
    public int AssessmentTypeId { get; set; }
    public int CloId { get; set; }
    public decimal? Percentage { get; set; }
}

public class CloTypeContributionDto
{
    public string AssessmentTypeName { get; set; } = string.Empty;
    public decimal DesignSharePercent { get; set; }
    public decimal? LocalAttainmentPercent { get; set; }
    public decimal WeightedContribution { get; set; }
    public string? Note { get; set; }
}

public class CloAttainmentResultItemDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public decimal AttainmentPercentage { get; set; }
    public List<CloTypeContributionDto> Contributions { get; set; } = new();
}

public class CloAttainmentResultsDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public DateTime? CalculatedAt { get; set; }
    public List<CloAttainmentResultItemDto> Results { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class CalculateCloAttainmentDto
{
    public int TeacherUserId { get; set; }
}

public class PloCloContributionDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public decimal MappingPercent { get; set; }
    public decimal? CloAttainmentPercent { get; set; }
    public decimal WeightedContribution { get; set; }
    public string? Note { get; set; }
}

public class PloAttainmentResultItemDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
    public decimal AttainmentPercentage { get; set; }
    public List<PloCloContributionDto> Contributions { get; set; } = new();
}

public class PloAttainmentResultsDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public DateTime? CalculatedAt { get; set; }
    public bool HasCloPloMapping { get; set; }
    public List<PloAttainmentResultItemDto> Results { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class SubjectAttainmentBundleDto
{
    public CloAttainmentResultsDto Clo { get; set; } = new();
    public PloAttainmentResultsDto Plo { get; set; } = new();
}

public class CloProgressChartItemDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    public decimal AttainmentPercentage { get; set; }
    public bool IsMapped { get; set; }
    public List<CloTypeContributionDto> MappedBreakdown { get; set; } = new();
}

public class PloProgressChartItemDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
    public decimal AttainmentPercentage { get; set; }
    public bool IsMapped { get; set; }
    public List<PloCloContributionDto> MappedBreakdown { get; set; } = new();
}

public class SubjectAttainmentDashboardDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public DateTime? CalculatedAt { get; set; }
    public bool HasCloTaskMapping { get; set; }
    public bool HasCloPloMapping { get; set; }
    /// <summary>Task×CLO design (for Results matrices).</summary>
    public CloAssessmentDesignDto Design { get; set; } = new();
    /// <summary>Live marks-based local/weighted cells for Results matrices (not question coverage).</summary>
    public List<AssessmentFolderCompletionCellDto> AttainmentCells { get; set; } = new();
    public List<CloProgressChartItemDto> CloProgress { get; set; } = new();
    public List<PloProgressChartItemDto> PloProgress { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Master Folder → Assessment: design matrix, question-coverage completion, and counts.
/// </summary>
public class AssessmentFolderTypeCountDto
{
    public int AssessmentTypeId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AssessmentFolderCompletionCellDto
{
    public int AssessmentTypeId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    /// <summary>CLO and Task design share for this type×CLO (e.g. 30).</summary>
    public decimal DesignSharePercent { get; set; }
    /// <summary>
    /// Assessment: question coverage of this type’s share (0–100).
    /// Results: student local attainment on that type×CLO (0–100).
    /// </summary>
    public decimal? LocalAttainmentPercent { get; set; }
    /// <summary>Local value scaled by design share (e.g. 30 if local=100 and share=30).</summary>
    public decimal WeightedContribution { get; set; }
    public string? Note { get; set; }
}

public class AssessmentFolderCloSummaryDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    /// <summary>Σ weighted contributions (original-share context).</summary>
    public decimal OverallWeightedPercent { get; set; }
    /// <summary>Average of local attainments across mapped types that have data.</summary>
    public decimal? OverallLocalPercent { get; set; }
}

public class AssessmentFolderOverviewDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public CloAssessmentDesignDto Design { get; set; } = new();
    public List<AssessmentFolderCompletionCellDto> CompletionCells { get; set; } = new();
    public List<AssessmentFolderCloSummaryDto> CloSummaries { get; set; } = new();
    public List<AssessmentFolderTypeCountDto> AssessmentCounts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class StudentPloDetailItemDto
{
    public string Label { get; set; } = string.Empty;
    public int? CloId { get; set; }
    public string? CloCode { get; set; }
    public string CloDescription { get; set; } = string.Empty;
    public decimal Obtained { get; set; }
    public decimal Maximum { get; set; }
    public decimal? Percent { get; set; }
    public decimal MappingPercent { get; set; }
    public decimal? AttainmentPercent { get; set; }
    public bool Failed { get; set; }
}

public class StudentPloScoreDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
    public decimal? Percent { get; set; }
    public bool Failed { get; set; }
    public List<StudentPloDetailItemDto> Detail { get; set; } = new();
    public List<string> ClosToPass { get; set; } = new();
    public string PassAdvice { get; set; } = string.Empty;
}

public class StudentPloResultRowDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public List<StudentPloScoreDto> Plos { get; set; } = new();
    public List<string> FailedPloCodes { get; set; } = new();
    public List<string> ClosToPass { get; set; } = new();
    public bool FailedByPlo { get; set; }
    public string PassAdvice { get; set; } = string.Empty;
}

public class StudentPloResultsDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public decimal FailThresholdPercent { get; set; } = 50m;
    public List<StudentPloScoreDto> PloColumns { get; set; } = new();
    public List<StudentPloResultRowDto> Students { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

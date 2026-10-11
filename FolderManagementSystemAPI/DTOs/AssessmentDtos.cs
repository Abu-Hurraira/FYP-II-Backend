namespace FolderManagementSystemAPI.DTOs;

public class AssessmentListItemDto
{
    public int AssessmentId { get; set; }
    public int OfferedCourseId { get; set; }
    public int AssessmentTypeId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public int? AssessmentNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public decimal? TotalMarks { get; set; }
    public int QuestionCount { get; set; }
    public bool IsEmpty { get; set; }
    /// <summary>True after the teacher completes setup; unlocks the section mark sheet.</summary>
    public bool IsSetupComplete { get; set; }
    /// <summary>True for the default required slots (8 quizzes / 4 assignments / 1 mid / 1 final).</summary>
    public bool IsRequired { get; set; }
    /// <summary>Distinct CLO codes mapped via any question on this assessment.</summary>
    public List<string> CloCodes { get; set; } = new();
}

public class AssessmentQuestionCloDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    /// <summary>Question coverage of this assessment-type bucket for the CLO (0–100).</summary>
    public decimal Percentage { get; set; }
    /// <summary>CLO and Task design share for this assessment type (e.g. Quiz = 30% of CLO).</summary>
    public decimal DesignSharePercent { get; set; }
    /// <summary>Effective CLO contribution = Percentage × DesignSharePercent / 100.</summary>
    public decimal EffectiveCloPercent { get; set; }
}

public class AssessmentQuestionCloInputDto
{
    public int CloId { get; set; }
    /// <summary>0–100 of the assessment-type share for this CLO.</summary>
    public decimal Percentage { get; set; }
}

public class AssessmentQuestionDto
{
    public int QuestionId { get; set; }
    public int? ParentQuestionId { get; set; }
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal? Marks { get; set; }
    public List<int> CloIds { get; set; } = new();
    public List<string> CloCodes { get; set; } = new();
    public List<AssessmentQuestionCloDto> CloMappings { get; set; } = new();
    public List<AssessmentQuestionDto> Parts { get; set; } = new();
}

public class AssessmentDetailDto : AssessmentListItemDto
{
    public List<AssessmentQuestionDto> Questions { get; set; } = new();
    public AssessmentLinkedFileDto? QuestionPaperFile { get; set; }
    public AssessmentLinkedFileDto? SolutionFile { get; set; }
    public AssessmentLinkedFileDto? BestSampleFile { get; set; }
    public AssessmentLinkedFileDto? AverageSampleFile { get; set; }
    public AssessmentLinkedFileDto? WorstSampleFile { get; set; }
    public int SampleUploadCount { get; set; }
}

public class AssessmentLinkedFileDto
{
    public int AssessmentFileId { get; set; }
    public int FileId { get; set; }
    public string FilePurpose { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileCategory { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public class CreateAssessmentQuestionDto
{
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal? Marks { get; set; }
    public List<int> CloIds { get; set; } = new();
    public List<AssessmentQuestionCloInputDto> CloMappings { get; set; } = new();
}

public class AddAssessmentQuestionDto
{
    public int TeacherUserId { get; set; }
    public int? ParentQuestionId { get; set; }
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal? Marks { get; set; }
    public List<int> CloIds { get; set; } = new();
    public List<AssessmentQuestionCloInputDto> CloMappings { get; set; } = new();
}

public class UpdateAssessmentQuestionDto
{
    public int TeacherUserId { get; set; }
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal? Marks { get; set; }
    public List<int> CloIds { get; set; } = new();
    public List<AssessmentQuestionCloInputDto> CloMappings { get; set; } = new();
}

public class CreateAssessmentDto
{
    public int TeacherUserId { get; set; }
    public int OfferedCourseId { get; set; }
    public string AssessmentTypeName { get; set; } = "Assignment";
    public int? AssessmentNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public decimal? TotalMarks { get; set; }
    public List<CreateAssessmentQuestionDto> Questions { get; set; } = new();
}

public class UpdateAssessmentMetaDto
{
    public int TeacherUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public decimal TotalMarks { get; set; }
}

public class CompleteAssessmentSetupDto
{
    public int TeacherUserId { get; set; }
}

public class AssessmentAvailableCloDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
    public string CloDescription { get; set; } = string.Empty;
    /// <summary>Approved CLO and Task share for the requested assessment type.</summary>
    public decimal DesignSharePercent { get; set; }
    /// <summary>Local coverage already used across this type's questions (0–100 of the type share).</summary>
    public decimal UsedLocalPercent { get; set; }
    /// <summary>Remaining local coverage for this type (100 − UsedLocalPercent).</summary>
    public decimal RemainingLocalPercent { get; set; }
}

public class AssessmentFileUploadResultDto
{
    public string Message { get; set; } = string.Empty;
    public AssessmentDetailDto Assessment { get; set; } = null!;
}

public class AssessmentLoadSchemeDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public string? AcademicYear { get; set; }
    public DateTime? StartDate { get; set; }
    public int AssessmentCount { get; set; }
}

public class AssessmentLoadItemDto
{
    public int AssessmentId { get; set; }
    public int? AssessmentNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal? TotalMarks { get; set; }
    public int QuestionCount { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public List<string> CloCodes { get; set; } = new();
    public bool AlreadyInCurrent { get; set; }
}

public class AssessmentLoadSchemeDetailDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public string AssessmentTypeName { get; set; } = string.Empty;
    public List<AssessmentLoadItemDto> Items { get; set; } = new();
}

public class ApplyAssessmentsFromSchemeDto
{
    public int TeacherUserId { get; set; }
    public int SourceSessionId { get; set; }
    public string AssessmentTypeName { get; set; } = string.Empty;
    public List<int> AssessmentIds { get; set; } = new();
}

public class ApplyAssessmentsFromSchemeResultDto
{
    public int CreatedCount { get; set; }
    public int FilledCount { get; set; }
    public int SkippedCount { get; set; }
    public int MappedCloCount { get; set; }
    public int UnmappedCloSkipped { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AssessmentSummaryDto
{
    public int ProgramCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<AssessmentSummaryCloDto> Clos { get; set; } = new();
    public List<string> CloCodes { get; set; } = new();
    public List<AssessmentSummaryRowDto> Rows { get; set; } = new();
}

public class ParsedQuestionPaperDto
{
    public string Message { get; set; } = string.Empty;
    public List<ParsedQuestionDraftDto> Questions { get; set; } = new();
}

public class ParsedQuestionDraftDto
{
    public string QuestionNo { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public decimal? Marks { get; set; }
    public List<AssessmentQuestionCloDto> CloMappings { get; set; } = new();
    public List<ParsedQuestionDraftDto> Parts { get; set; } = new();
}

public class AssessmentSummaryCloDto
{
    public int CloId { get; set; }
    public string CloCode { get; set; } = string.Empty;
}

public class AssessmentSummaryRowDto
{
    public string TaskName { get; set; } = string.Empty;
    public int AssessmentTypeId { get; set; }
    public Dictionary<string, decimal> DesignShares { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CompletedLocal { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CompletedTotals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> RemainingLocal { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> RemainingTotals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

namespace FolderManagementSystemAPI.DTOs;

/// <summary>Fixed course totals derived from credit hours + lab flag.</summary>
public class CourseFixedMarkingDto
{
    public int ClassCreditHours { get; set; }
    public int LabCreditHours { get; set; }
    public int TotalCreditHours { get; set; }
    public bool HasLab { get; set; }
    public string ProfileLabel { get; set; } = string.Empty;
    public decimal TotalMarks { get; set; }
    public decimal HomeworkMarks { get; set; }
    public decimal MidMarks { get; set; }
    public decimal LabMarks { get; set; }
    public decimal FinalMarks { get; set; }
}

public class CourseResultSchemeDto
{
    public CourseFixedMarkingDto Fixed { get; set; } = new();
    public int AssignmentBestOf { get; set; } = 3;
    public int QuizBestOf { get; set; } = 6;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastGeneratedAt { get; set; }

    // Convenience mirrors for UI/result rows.
    public decimal TotalMarks => Fixed.TotalMarks;
    public decimal HomeworkMarks => Fixed.HomeworkMarks;
    public decimal MidMarks => Fixed.MidMarks;
    public decimal LabMarks => Fixed.LabMarks;
    public decimal FinalMarks => Fixed.FinalMarks;
    public bool HasLab => Fixed.HasLab;
}

public class SaveCourseResultBestOfDto
{
    public int TeacherUserId { get; set; }
    public int AssignmentBestOf { get; set; } = 3;
    public int QuizBestOf { get; set; } = 6;
}

public class MarkSchemeExtraItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Marks { get; set; }
}

public class LabMarkSchemeDto
{
    public int OfferedCourseId { get; set; }
    public decimal TargetMarks { get; set; } = 20;
    public decimal QuizMarks { get; set; }
    public decimal AssignmentMarks { get; set; }
    public decimal LabTaskMarks { get; set; }
    public decimal ProjectMarks { get; set; }
    public decimal AttendanceMarks { get; set; }
    public decimal ExamMarks { get; set; }
    public List<MarkSchemeExtraItemDto> ExtraItems { get; set; } = new();
    public decimal ExtraMarksTotal { get; set; }
    public decimal AssignedTotal { get; set; }
    public decimal RemainingMarks { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveLabMarkSchemeDto
{
    public int TeacherUserId { get; set; }
    public decimal QuizMarks { get; set; }
    public decimal AssignmentMarks { get; set; }
    public decimal LabTaskMarks { get; set; }
    public decimal ProjectMarks { get; set; }
    public decimal AttendanceMarks { get; set; }
    public decimal ExamMarks { get; set; }
    public List<MarkSchemeExtraItemDto>? ExtraItems { get; set; }
}

public class HomeworkMarkSchemeDto
{
    public int OfferedCourseId { get; set; }
    public decimal TargetMarks { get; set; }
    public decimal AttendanceMarks { get; set; }
    public decimal QuizMarks { get; set; }
    public decimal AssignmentMarks { get; set; }
    public List<MarkSchemeExtraItemDto> ExtraItems { get; set; } = new();
    public decimal ExtraMarksTotal { get; set; }
    public decimal AssignedTotal { get; set; }
    public decimal RemainingMarks { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveHomeworkMarkSchemeDto
{
    public int TeacherUserId { get; set; }
    public decimal AttendanceMarks { get; set; }
    public decimal QuizMarks { get; set; }
    public decimal AssignmentMarks { get; set; }
    public List<MarkSchemeExtraItemDto>? ExtraItems { get; set; }
}

public class CourseResultReadinessItemDto
{
    public int AssessmentId { get; set; }
    public int? AssessmentNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsSetupComplete { get; set; }
}

public class CourseResultReadinessGroupDto
{
    public string AssessmentTypeName { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int CompletedCount { get; set; }
    public bool IsComplete { get; set; }
    public List<CourseResultReadinessItemDto> Items { get; set; } = new();
}

public class CourseResultReadinessDto
{
    public int OfferedCourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int ClassCreditHours { get; set; }
    public int LabCreditHours { get; set; }
    public int TotalCreditHours { get; set; }
    public string CreditHoursDisplay { get; set; } = string.Empty;
    public bool HasLab { get; set; }
    public int StudentCount { get; set; }
    public bool CanGenerate { get; set; }
    public bool HomeworkSchemeComplete { get; set; }
    public bool LabSchemeComplete { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<CourseResultReadinessGroupDto> RequiredGroups { get; set; } = new();
    public CourseResultSchemeDto Scheme { get; set; } = new();
    public HomeworkMarkSchemeDto? HomeworkScheme { get; set; }
    public LabMarkSchemeDto? LabScheme { get; set; }
}

public class CourseResultStudentRowDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public decimal HomeworkMarks { get; set; }
    public decimal MidMarks { get; set; }
    public decimal LabMarks { get; set; }
    public decimal FinalMarks { get; set; }
    public decimal TotalObtained { get; set; }
    public decimal TotalMax { get; set; }
    public decimal Percentage { get; set; }
    public decimal TheoryTotal { get; set; }
    public decimal PracScore { get; set; }
    public decimal GrandTotal { get; set; }
    public string InWords { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public decimal QPoint { get; set; }
    public List<int> SelectedAssignmentIds { get; set; } = new();
    public List<int> SelectedQuizIds { get; set; } = new();
}

public class CourseResultGridDto
{
    public int OfferedCourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int ClassCreditHours { get; set; }
    public int LabCreditHours { get; set; }
    public int TotalCreditHours { get; set; }
    public string CreditHoursDisplay { get; set; } = string.Empty;
    public DateTime? GeneratedAt { get; set; }
    public int? GeneratedByUserId { get; set; }
    public CourseResultSchemeDto? Scheme { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<CourseResultStudentRowDto> Students { get; set; } = new();
}

public class GenerateCourseResultDto
{
    public int TeacherUserId { get; set; }
}

public class ComponentMarksExtraColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal MaxMarks { get; set; }
}

public class ComponentMarksStudentRowDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public decimal? AttendancePercent { get; set; }
    public decimal? LabTaskObtained { get; set; }
    public decimal? ProjectObtained { get; set; }
    public Dictionary<string, decimal?> ExtraObtained { get; set; } = new();
}

public class ComponentMarksSheetDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public bool HasLab { get; set; }
    public decimal HomeworkAttendanceMax { get; set; }
    public decimal LabAttendanceMax { get; set; }
    public decimal LabTaskMax { get; set; }
    public decimal ProjectMax { get; set; }
    public bool ShowAttendance { get; set; }
    public bool ShowLabTasks { get; set; }
    public bool ShowProject { get; set; }
    public List<ComponentMarksExtraColumnDto> ExtraColumns { get; set; } = new();
    public List<ComponentMarksStudentRowDto> Students { get; set; } = new();
}

public class SaveComponentMarksEntryDto
{
    public int StudentId { get; set; }
    public decimal? AttendancePercent { get; set; }
    public decimal? LabTaskObtained { get; set; }
    public decimal? ProjectObtained { get; set; }
    public Dictionary<string, decimal?>? ExtraObtained { get; set; }
}

public class SaveComponentMarksDto
{
    public int TeacherUserId { get; set; }
    public List<SaveComponentMarksEntryDto> Entries { get; set; } = new();
}

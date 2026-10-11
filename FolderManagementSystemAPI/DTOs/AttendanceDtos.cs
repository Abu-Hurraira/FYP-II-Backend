namespace FolderManagementSystemAPI.DTOs;

public class AttendanceSheetDto
{
    public int OfferedCourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string? AttendanceSheetPath { get; set; }
    public List<string> Dates { get; set; } = new();
    public List<AttendanceStudentRowDto> Students { get; set; } = new();
}

public class AttendanceStudentRowDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AridNo { get; set; } = string.Empty;
    public List<AttendanceCellDto> Cells { get; set; } = new();
}

public class AttendanceCellDto
{
    public int? AttendanceId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class AttendanceCellUpdateDto
{
    public int StudentId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public class UpdateAttendanceSheetDto
{
    public int TeacherUserId { get; set; }
    public List<AttendanceCellUpdateDto> Updates { get; set; } = new();
}

public class AttendanceImportResultDto
{
    public string Message { get; set; } = string.Empty;
    public int UpsertedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public AttendanceSheetDto? Sheet { get; set; }
}

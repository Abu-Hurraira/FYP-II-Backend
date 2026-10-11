namespace FolderManagementSystemAPI.DTOs;

public class TeacherPermissionFlagsDto
{
    public bool CanManageClos { get; set; }
    public bool CanUploadWeeklyContent { get; set; }
    public bool CanUploadMidExamQuestions { get; set; }
    public bool CanUploadFinalExamQuestions { get; set; }
    public bool CanManageCloPloMapping { get; set; }
    public bool CanManageCloTaskMapping { get; set; }

    // Backward-compatible aliases used by older clients
    public bool CanUploadCourseContent
    {
        get => CanUploadWeeklyContent;
        set => CanUploadWeeklyContent = value;
    }

    public bool CanUploadExamQuestions
    {
        get => CanUploadMidExamQuestions || CanUploadFinalExamQuestions;
        set
        {
            CanUploadMidExamQuestions = value;
            CanUploadFinalExamQuestions = value;
        }
    }
}

public class HodTeacherPermissionRowDto
{
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int OfferedCourseId { get; set; }
    public bool IsSectionA { get; set; }
    public bool CanManageClos { get; set; }
    public bool CanUploadWeeklyContent { get; set; }
    public bool CanUploadMidExamQuestions { get; set; }
    public bool CanUploadFinalExamQuestions { get; set; }
    public bool CanManageCloPloMapping { get; set; }
    public bool CanManageCloTaskMapping { get; set; }

    public bool CanUploadCourseContent
    {
        get => CanUploadWeeklyContent;
        set => CanUploadWeeklyContent = value;
    }

    public bool CanUploadExamQuestions
    {
        get => CanUploadMidExamQuestions || CanUploadFinalExamQuestions;
        set
        {
            CanUploadMidExamQuestions = value;
            CanUploadFinalExamQuestions = value;
        }
    }
}

public class HodTeacherPermissionGroupDto
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int ProgramCourseId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public List<HodTeacherPermissionRowDto> Teachers { get; set; } = new();
}

public class UpdateTeacherPermissionDto
{
    public int HodUserId { get; set; }
    public string PermissionType { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}

/// <summary>Lightweight toggle response — clients already patch UI optimistically.</summary>
public class UpdateTeacherPermissionResultDto
{
    public bool Ok { get; set; } = true;
    public int ProgramCourseId { get; set; }
    public string PermissionType { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}

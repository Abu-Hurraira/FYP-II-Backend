namespace FolderManagementSystemAPI.Services;

public static class TeacherPermissionTypes
{
    public const string ManageClos = "Manage CLOs";
    public const string UploadWeeklyContent = "Upload Weekly Content";
    public const string UploadMidExamQuestions = "Upload Mid Exam Questions";
    public const string UploadFinalExamQuestions = "Upload Final Exam Questions";
    public const string ManageCloPloMapping = "Manage CLO-PLO Mapping";
    public const string ManageCloTaskMapping = "Manage CLO-Task Mapping";

    // Legacy names kept for migration only
    public const string LegacyUploadCourseContent = "Upload Course Content";
    public const string LegacyUploadExamQuestions = "Upload Exam Questions";

    public static readonly string[] All =
    [
        ManageClos,
        UploadWeeklyContent,
        UploadMidExamQuestions,
        UploadFinalExamQuestions,
        ManageCloPloMapping,
        ManageCloTaskMapping
    ];
}

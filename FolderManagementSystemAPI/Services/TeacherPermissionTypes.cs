namespace FolderManagementSystemAPI.Services;

public static class TeacherPermissionTypes
{
    public const string ManageClos = "Manage CLOs";
    public const string UploadCourseContent = "Upload Course Content";
    public const string UploadExamQuestions = "Upload Exam Questions";

    public static readonly string[] All =
    [
        ManageClos,
        UploadCourseContent,
        UploadExamQuestions
    ];
}

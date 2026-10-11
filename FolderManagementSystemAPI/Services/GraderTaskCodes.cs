namespace FolderManagementSystemAPI.Services;

public static class GraderTaskCodes
{
    public const string Attendance = "Attendance";
    public const string AssignmentMarks = "AssignmentMarks";
    public const string QuizMarks = "QuizMarks";
    public const string ComponentMarks = "ComponentMarks";

    public static readonly string[] All =
    [
        Attendance,
        AssignmentMarks,
        QuizMarks,
        ComponentMarks
    ];

    public static string Label(string? taskCode) => taskCode switch
    {
        Attendance => "Attendance",
        AssignmentMarks => "Assignment marks",
        QuizMarks => "Quiz marks",
        ComponentMarks => "Component marks",
        _ => taskCode ?? string.Empty
    };

    public static bool IsValid(string? taskCode) =>
        All.Contains(taskCode, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? taskCode)
    {
        if (string.IsNullOrWhiteSpace(taskCode))
        {
            return string.Empty;
        }

        return All.FirstOrDefault(x => x.Equals(taskCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? taskCode.Trim();
    }
}

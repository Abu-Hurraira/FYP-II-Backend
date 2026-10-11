using FolderManagementSystemAPI.DTOs;

namespace FolderManagementSystemAPI.Services;

public static class FolderProgressHelper
{
    public static Dictionary<string, int> BuildCategoryCountLookup(
        IEnumerable<(string TemplateCode, string Category, int Count)> rows)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Category))
            {
                continue;
            }

            counts[$"{row.TemplateCode}|{row.Category}"] = row.Count;
        }

        return counts;
    }

    public static int Count(Dictionary<string, int> counts, string code, string category)
    {
        return counts.TryGetValue($"{code}|{category}", out var value) ? value : 0;
    }

    public static FolderItemProgressResult BuildFolderProgress(
        string templateCode,
        Dictionary<string, int> counts)
    {
        var categories = new List<FolderCategoryProgressDto>();
        var required = 0;
        var uploaded = 0;
        var isComplete = true;

        void AddCategory(string category, int minimum)
        {
            var current = Count(counts, templateCode, category);
            categories.Add(new FolderCategoryProgressDto
            {
                Category = category,
                Required = minimum,
                Uploaded = current
            });
            required += minimum;
            uploaded += Math.Min(current, minimum);
            if (current < minimum)
            {
                isComplete = false;
            }
        }

        switch (templateCode.ToLowerInvariant())
        {
            case "g":
                AddCategory("Assignment Question", 4);
                AddCategory("Quiz Question", 8);
                AddCategory("Mid Exam Question", 1);
                AddCategory("Final Exam Question", 1);
                break;
            case "h":
                AddCategory("Assignment Solution", 4);
                AddCategory("Quiz Solution", 8);
                AddCategory("Mid Exam Solution", 1);
                AddCategory("Final Exam Solution", 1);
                break;
            case "i":
                AddCategory("Assignment Best Sample", 1);
                AddCategory("Assignment Average Sample", 1);
                AddCategory("Assignment Worst Sample", 1);
                AddCategory("Quiz Best Sample", 1);
                AddCategory("Quiz Average Sample", 1);
                AddCategory("Quiz Worst Sample", 1);
                AddCategory("Mid Exam Best Sample", 1);
                AddCategory("Mid Exam Average Sample", 1);
                AddCategory("Mid Exam Worst Sample", 1);
                AddCategory("Final Exam Best Sample", 1);
                AddCategory("Final Exam Average Sample", 1);
                AddCategory("Final Exam Worst Sample", 1);
                break;
            default:
                var defaultCategory = GetDefaultCategoryForTemplate(templateCode);
                if (!string.IsNullOrWhiteSpace(defaultCategory))
                {
                    AddCategory(defaultCategory, 1);
                }
                break;
        }

        var summary = templateCode.ToLowerInvariant() switch
        {
            "g" => BuildPairSummary(categories, "Assignment Question", "Quiz Question"),
            "h" => BuildPairSummary(categories, "Assignment Solution", "Quiz Solution"),
            "i" => $"{uploaded}/{required} samples",
            _ => $"{uploaded}/{Math.Max(required, 1)} files"
        };

        return new FolderItemProgressResult
        {
            RequiredFileCount = Math.Max(required, 1),
            UploadedFileCount = uploaded,
            IsComplete = isComplete,
            ProgressSummary = summary,
            CategoryProgress = categories
        };
    }

    public static List<string> BuildMissingPreview(Dictionary<string, int> counts)
    {
        var missing = new List<string>();

        void RequireAtLeastOne(string code, string folderName, string category)
        {
            if (Count(counts, code, category) < 1)
            {
                missing.Add($"{code}-{folderName}: missing {category}.");
            }
        }

        void RequireMinimum(string code, string folderName, string category, int minimum)
        {
            var current = Count(counts, code, category);
            if (current < minimum)
            {
                missing.Add($"{code}-{folderName}: {current}/{minimum} {category}.");
            }
        }

        RequireAtLeastOne("a", "Course Objectives", "Course Objective");
        RequireAtLeastOne("b", "CLO-GA-Mapping", "CLO-GA Mapping");
        RequireAtLeastOne("c", "Course Contents", "Course Content");
        RequireAtLeastOne("d", "Weekly Plan", "Weekly Plan");
        RequireAtLeastOne("e", "Attendance Record", "Attendance Record");
        RequireAtLeastOne("f", "Reference Material", "Reference Material");

        RequireMinimum("g", "Quiz-Assignments-Exams", "Assignment Question", 4);
        RequireMinimum("g", "Quiz-Assignments-Exams", "Quiz Question", 8);
        RequireMinimum("g", "Quiz-Assignments-Exams", "Mid Exam Question", 1);
        RequireMinimum("g", "Quiz-Assignments-Exams", "Final Exam Question", 1);

        RequireMinimum("h", "Solutions", "Assignment Solution", 4);
        RequireMinimum("h", "Solutions", "Quiz Solution", 8);
        RequireMinimum("h", "Solutions", "Mid Exam Solution", 1);
        RequireMinimum("h", "Solutions", "Final Exam Solution", 1);

        RequireAtLeastOne("i", "Samples", "Assignment Best Sample");
        RequireAtLeastOne("i", "Samples", "Assignment Average Sample");
        RequireAtLeastOne("i", "Samples", "Assignment Worst Sample");
        RequireAtLeastOne("i", "Samples", "Quiz Best Sample");
        RequireAtLeastOne("i", "Samples", "Quiz Average Sample");
        RequireAtLeastOne("i", "Samples", "Quiz Worst Sample");
        RequireAtLeastOne("i", "Samples", "Mid Exam Best Sample");
        RequireAtLeastOne("i", "Samples", "Mid Exam Average Sample");
        RequireAtLeastOne("i", "Samples", "Mid Exam Worst Sample");
        RequireAtLeastOne("i", "Samples", "Final Exam Best Sample");
        RequireAtLeastOne("i", "Samples", "Final Exam Average Sample");
        RequireAtLeastOne("i", "Samples", "Final Exam Worst Sample");

        RequireAtLeastOne("j", "Marks Distribution and Grading", "Marks Distribution and Grading");
        RequireAtLeastOne("k", "Result", "Result");
        RequireAtLeastOne("l", "Outcomes Assessment", "Outcomes Assessment");
        RequireAtLeastOne("m", "Technology Involved", "Technology Involved");
        RequireAtLeastOne("n", "Design Techniques Practiced", "Design Techniques Practiced");
        RequireAtLeastOne("o", "FCAR Report", "Complete Analysis of Course");

        return missing;
    }

    private static string BuildPairSummary(
        List<FolderCategoryProgressDto> categories,
        string firstCategory,
        string secondCategory)
    {
        var first = categories.FirstOrDefault(c => c.Category == firstCategory);
        var second = categories.FirstOrDefault(c => c.Category == secondCategory);

        return $"Assign {first?.Uploaded ?? 0}/{first?.Required ?? 0} · Quiz {second?.Uploaded ?? 0}/{second?.Required ?? 0}";
    }

    private static string? GetDefaultCategoryForTemplate(string templateCode)
    {
        return templateCode.ToLowerInvariant() switch
        {
            "a" => "Course Objective",
            "b" => "CLO-GA Mapping",
            "c" => "Course Content",
            "d" => "Weekly Plan",
            "e" => "Attendance Record",
            "f" => "Reference Material",
            "j" => "Marks Distribution and Grading",
            "k" => "Result",
            "l" => "Outcomes Assessment",
            "m" => "Technology Involved",
            "n" => "Design Techniques Practiced",
            "o" => "Complete Analysis of Course", // stored category; folder displays as FCAR Report
            _ => null
        };
    }

    public sealed class FolderItemProgressResult
    {
        public int RequiredFileCount { get; set; }
        public int UploadedFileCount { get; set; }
        public bool IsComplete { get; set; }
        public string ProgressSummary { get; set; } = string.Empty;
        public List<FolderCategoryProgressDto> CategoryProgress { get; set; } = new();
    }
}

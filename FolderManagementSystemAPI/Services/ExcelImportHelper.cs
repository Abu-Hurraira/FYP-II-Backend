using ClosedXML.Excel;

namespace FolderManagementSystemAPI.Services;

/// <summary>
/// Shared Excel header/value matching so offerings and enrollment files agree.
/// </summary>
public static class ExcelImportHelper
{
    public static readonly string[] CourseCodeHeaders =
    {
        "coursecode", "coursen", "courseno", "course_n", "code", "subjectcode"
    };

    public static readonly string[] CourseTitleHeaders =
    {
        "coursetitle", "coursedesc", "course_desc", "coursedescription",
        "course", "coursename", "subject", "title"
    };

    public static readonly string[] SectionHeaders = { "section", "sectionname" };

    public static string NormalizeHeader(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }

    public static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
            {
                map[header] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    public static int GetColumnIndex(Dictionary<string, int> headerMap, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var normalized = NormalizeHeader(name);
            if (headerMap.TryGetValue(normalized, out var index))
            {
                return index;
            }
        }

        return 0;
    }

    public static int FindColumn(IXLRow headerRow, params string[] possibleNames)
    {
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = NormalizeHeader(headerRow.Cell(col).GetString());
            foreach (var name in possibleNames)
            {
                if (header == NormalizeHeader(name))
                {
                    return col;
                }
            }
        }

        return 0;
    }

    public static string GetCellValue(IXLRow row, int columnNumber)
    {
        return columnNumber <= 0
            ? string.Empty
            : row.Cell(columnNumber).GetFormattedString().Trim();
    }

    /// <summary>
    /// Match catalog codes across CSC-303 / CSC303 / csc_303.
    /// </summary>
    public static string CourseMatchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToUpperInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }

    public static bool CourseEquals(string? left, string? right)
    {
        var a = CourseMatchKey(left);
        var b = CourseMatchKey(right);
        return a.Length > 0 && a == b;
    }
}

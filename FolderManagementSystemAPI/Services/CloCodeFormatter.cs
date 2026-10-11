using System.Text.RegularExpressions;

namespace FolderManagementSystemAPI.Services;

/// <summary>
/// Canonical CLO code: CLO{n} with no hyphen, e.g. CLO1.
/// CLO-1, clo 1, CLO01 all become CLO1.
/// </summary>
public static class CloCodeFormatter
{
    public const string FormatHint = "CLO1";

    private static readonly Regex Pattern = new(
        @"^CLO(?<n>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Canonical(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var compact = new string(raw.Trim().ToUpperInvariant()
            .Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            .ToArray());

        var match = Pattern.Match(compact);
        if (!match.Success)
        {
            return compact;
        }

        if (!int.TryParse(match.Groups["n"].Value, out var n) || n <= 0)
        {
            return compact;
        }

        return $"CLO{n}";
    }

    public static string NextCode(int number) => $"CLO{Math.Max(1, number)}";

    public static int TrailingNumber(string? raw)
    {
        var canonical = Canonical(raw);
        var match = Pattern.Match(canonical);
        return match.Success && int.TryParse(match.Groups["n"].Value, out var n) ? n : 0;
    }

    public static bool Equals(string? left, string? right)
    {
        var a = Canonical(left);
        var b = Canonical(right);
        return a.Length > 0 && a == b;
    }
}

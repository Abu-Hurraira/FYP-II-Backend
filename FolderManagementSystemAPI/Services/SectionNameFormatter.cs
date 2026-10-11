using System.Text.RegularExpressions;

namespace FolderManagementSystemAPI.Services;

/// <summary>
/// Canonical section name: {PROGRAM}-{SEMESTER}{LETTER}, e.g. BSCS-1A.
/// Semester is the number; the last letter is the section.
/// </summary>
public static class SectionNameFormatter
{
    public const string FormatHint = "PROGRAM-SEMESTERLETTER (e.g. BSCS-1A)";

    private static readonly Regex ParsedPattern = new(
        @"^(?:SECTION)?(?<prefix>[A-Z]*)(?<semester>\d{1,2})(?<letter>[A-Z])$",
        RegexOptions.Compiled);

    /// <summary>Short campus prefixes that map onto a program code.</summary>
    private static readonly Dictionary<string, string> PrefixAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BCS"] = "BSCS",
        ["BSCS"] = "BSCS",
        ["BSE"] = "BSSE",
        ["BSSE"] = "BSSE",
        ["BAI"] = "BSAI",
        ["BSAI"] = "BSAI",
    };

    public sealed record ParsedSection(string CanonicalName, string ProgramPrefix, int Semester, char Letter);

    public static string MatchKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static bool IsSectionA(string? sectionName)
    {
        if (!TryParse(sectionName, preferredProgramCode: null, out var parsed, out _))
        {
            return false;
        }

        return parsed.Letter == 'A';
    }

    public static bool TryParse(
        string? raw,
        string? preferredProgramCode,
        out ParsedSection parsed,
        out string error)
    {
        parsed = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = $"Section is required. Use {FormatHint}.";
            return false;
        }

        var compact = Compact(raw);
        var match = ParsedPattern.Match(compact);

        if (!match.Success)
        {
            error =
                $"Section '{raw.Trim()}' is not {FormatHint}. " +
                "Accepted variants of the same value (BSCS-1A, BSCS 1A, BSCS-1-A, bscs1a) are normalized.";
            return false;
        }

        var semesterText = match.Groups["semester"].Value;
        if (!int.TryParse(semesterText, out var semester) || semester <= 0)
        {
            error = $"Section '{raw.Trim()}' has an invalid semester. Use {FormatHint}.";
            return false;
        }

        var letter = char.ToUpperInvariant(match.Groups["letter"].Value[0]);
        var prefix = (match.Groups["prefix"].Value ?? string.Empty).Trim().ToUpperInvariant();
        var preferred = (preferredProgramCode ?? string.Empty).Trim().ToUpperInvariant();

        prefix = ResolvePrefix(prefix, preferred);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            error =
                $"Section '{raw.Trim()}' is missing the program code. Use {FormatHint}.";
            return false;
        }

        parsed = new ParsedSection($"{prefix}-{semester}{letter}", prefix, semester, letter);
        return true;
    }

    public static string RequireCanonical(string? raw, string? preferredProgramCode)
    {
        if (!TryParse(raw, preferredProgramCode, out var parsed, out var error))
        {
            throw new ArgumentException(error);
        }

        return parsed.CanonicalName;
    }

    private static string ResolvePrefix(string prefix, string preferredProgramCode)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return preferredProgramCode;
        }

        if (string.IsNullOrWhiteSpace(preferredProgramCode))
        {
            return PrefixAliases.TryGetValue(prefix, out var aliased) ? aliased : prefix;
        }

        if (PrefixesMatch(prefix, preferredProgramCode))
        {
            return preferredProgramCode;
        }

        return PrefixAliases.TryGetValue(prefix, out var mapped) ? mapped : prefix;
    }

    private static bool PrefixesMatch(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leftCanon = PrefixAliases.TryGetValue(left, out var l) ? l : left;
        var rightCanon = PrefixAliases.TryGetValue(right, out var r) ? r : right;
        return string.Equals(leftCanon, rightCanon, StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string value)
    {
        var chars = value.Trim().ToUpperInvariant()
            .Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            .ToArray();
        return new string(chars);
    }
}

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FolderManagementSystemAPI.Services;

public static class QuestionPaperParser
{
    public sealed record DraftPart(string QuestionNo, string QuestionText, decimal? Marks, List<string> CloCodes);

    public sealed record DraftQuestion(
        string QuestionNo,
        string QuestionText,
        decimal? Marks,
        List<string> CloCodes,
        List<DraftPart> Parts);

    public sealed record ParseResult(string Message, List<DraftQuestion> Questions);

    public static ParseResult Parse(byte[] bytes, string fileName)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new ParseResult("The uploaded file is empty.", new List<DraftQuestion>());
        }

        var ext = Path.GetExtension(fileName ?? string.Empty);
        string text;
        try
        {
            if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                text = ReadDocxText(bytes);
            }
            else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                text = ReadPdfText(bytes);
            }
            else
            {
                text = Encoding.UTF8.GetString(bytes);
            }
        }
        catch
        {
            return new ParseResult("Could not read questions from this file. Add them below.", new List<DraftQuestion>());
        }

        var questions = SplitQuestions(text);
        if (questions.Count == 0)
        {
            return new ParseResult("Could not read questions — add them below.", questions);
        }

        return new ParseResult($"Read {questions.Count} question{(questions.Count == 1 ? "" : "s")}. Review CLO mapping before saving.", questions);
    }

    private static string ReadDocxText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = XDocument.Parse(reader.ReadToEnd());
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var sb = new StringBuilder();
        foreach (var paragraph in xml.Descendants(w + "p"))
        {
            var line = string.Concat(paragraph.Descendants(w + "t").Select(t => t.Value));
            if (!string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine(line.Trim());
            }
        }

        return sb.ToString();
    }

    private static string ReadPdfText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var sb = new StringBuilder();
        foreach (Match match in Regex.Matches(raw, @"\((?:\\.|[^\\)])+\)"))
        {
            var value = match.Value.Trim('(', ')').Replace("\\n", " ").Replace("\\r", " ");
            if (value.Length > 1 && value.Any(char.IsLetterOrDigit))
            {
                sb.Append(value).Append(' ');
            }
        }

        return sb.ToString();
    }

    private static List<DraftQuestion> SplitQuestions(string text)
    {
        var cleaned = (text ?? string.Empty).Replace("\r\n", "\n");
        var chunks = Regex.Split(
            cleaned,
            @"(?m)(?=^\s*(?:Q(?:uestion)?\s*)?(\d+)[\).:\-\s])",
            RegexOptions.IgnoreCase);

        var questions = new List<DraftQuestion>();
        foreach (var chunk in chunks)
        {
            var block = chunk.Trim();
            if (string.IsNullOrWhiteSpace(block))
            {
                continue;
            }

            var header = Regex.Match(block, @"^(?:Q(?:uestion)?\s*)?(\d+)[\).:\-\s]*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!header.Success)
            {
                continue;
            }

            var number = header.Groups[1].Value.Trim();
            var body = header.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(body) && questions.Count == 0)
            {
                continue;
            }

            var parts = SplitParts(body);
            if (parts.Count > 0)
            {
                questions.Add(new DraftQuestion(
                    $"Q{number}",
                    string.Empty,
                    parts.Sum(p => p.Marks ?? 0),
                    new List<string>(),
                    parts));
            }
            else
            {
                questions.Add(new DraftQuestion(
                    $"Q{number}",
                    StripMeta(body, out var marks, out var clos),
                    marks,
                    clos,
                    new List<DraftPart>()));
            }
        }

        return questions;
    }

    private static List<DraftPart> SplitParts(string body)
    {
        var chunks = Regex.Split(body, @"(?=\([a-z]\)|(?<=\s)[a-z]\))", RegexOptions.IgnoreCase);
        var parts = new List<DraftPart>();
        foreach (var chunk in chunks)
        {
            var match = Regex.Match(chunk.Trim(), @"^\(?([a-z])\)[\).\s]*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                continue;
            }

            var text = StripMeta(match.Groups[2].Value.Trim(), out var marks, out var clos);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            parts.Add(new DraftPart(match.Groups[1].Value.ToLowerInvariant(), text, marks, clos));
        }

        return parts.Count >= 2 ? parts : new List<DraftPart>();
    }

    private static string StripMeta(string text, out decimal? marks, out List<string> clos)
    {
        marks = null;
        clos = new List<string>();
        var working = text ?? string.Empty;

        var marksMatch = Regex.Match(working, @"\((\d+(?:\.\d+)?)\s*(?:marks?|m)?\)", RegexOptions.IgnoreCase);
        if (marksMatch.Success && decimal.TryParse(marksMatch.Groups[1].Value, out var parsedMarks))
        {
            marks = parsedMarks;
            working = working.Replace(marksMatch.Value, " ");
        }

        foreach (Match match in Regex.Matches(working, @"CLO\s*-?\s*(\d+)(?:\s+(\d+(?:\.\d+)?)%)?", RegexOptions.IgnoreCase))
        {
            clos.Add($"CLO{match.Groups[1].Value}");
            working = working.Replace(match.Value, " ");
        }

        working = Regex.Replace(working, @"\s+", " ").Trim();
        return working;
    }
}

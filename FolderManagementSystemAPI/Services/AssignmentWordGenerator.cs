using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderManagementSystemAPI.Services;

public static class AssignmentWordGenerator
{
    public sealed record QuestionBlock(
        string Number,
        string Text,
        string? Marks,
        string? Clos,
        IReadOnlyList<QuestionBlock> Parts);

    public static byte[] Generate(
        string courseCode,
        int assignmentNo,
        string title,
        DateOnly? dueDate,
        decimal? totalMarks,
        IReadOnlyList<QuestionBlock> questions,
        string kind = "Assignment")
    {
        var documentXml = BuildDocumentXml(
            courseCode,
            assignmentNo,
            title,
            dueDate,
            totalMarks,
            questions,
            kind);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(zip, "_rels/.rels", RelsXml);
            WriteEntry(zip, "word/_rels/document.xml.rels", DocumentRelsXml);
            WriteEntry(zip, "word/document.xml", documentXml);
        }

        return stream.ToArray();
    }

    public static string FileName(string courseCode, int assignmentNo, string kind = "Assignment")
    {
        var paper = string.IsNullOrWhiteSpace(kind) ? "Assignment" : kind.Trim();
        var prefix = string.IsNullOrWhiteSpace(courseCode) ? paper : courseCode.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            prefix = prefix.Replace(invalid, '-');
            paper = paper.Replace(invalid, '-');
        }

        return $"{prefix} {paper} - {assignmentNo}.docx";
    }

    private static void WriteEntry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string BuildDocumentXml(
        string courseCode,
        int assignmentNo,
        string title,
        DateOnly? dueDate,
        decimal? totalMarks,
        IReadOnlyList<QuestionBlock> questions,
        string kind)
    {
        var paper = string.IsNullOrWhiteSpace(kind) ? "Assignment" : kind.Trim();
        var topic = CleanTitle(title, assignmentNo, paper);
        var heading = string.IsNullOrWhiteSpace(topic)
            ? $"{courseCode} {paper}-{assignmentNo}"
            : $"{courseCode} {paper}-{assignmentNo}: {topic}";

        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>
            """);
        sb.Append(Paragraph(heading, bold: true, size: 28, after: 240));

        if (questions.Count == 0)
        {
            sb.Append(Paragraph("No questions have been added yet.", bold: false, size: 24, after: 120));
        }
        else
        {
            foreach (var question in questions)
            {
                AppendQuestion(sb, question, level: 0);
            }
        }

        if (totalMarks is > 0)
        {
            sb.Append(Paragraph($"Total Marks: {totalMarks.Value.ToString("0.##", CultureInfo.InvariantCulture)}", bold: true, size: 24, after: 80));
        }

        sb.Append(Paragraph("Submission Guidelines", bold: true, size: 24, after: 80));
        sb.Append(Paragraph(
            "All students must submit their own work. Copying from classmates or internet sources is strictly not allowed. If any similarity is found between submissions, marks will be deducted.",
            bold: false,
            size: 24,
            after: 80));

        if (dueDate.HasValue)
        {
            var dueText = dueDate.Value.ToString("dddd dd MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
            var paperLower = paper.ToLowerInvariant();
            sb.Append(Paragraph(
                $"Last date to submit the {paperLower} is {dueText}. Late submissions will not be accepted.",
                bold: false,
                size: 24,
                after: 80));
        }

        sb.Append("""
            <w:sectPr>
              <w:pgSz w:w="12240" w:h="15840"/>
              <w:pgMar w:top="1080" w:right="1080" w:bottom="1080" w:left="1080"/>
            </w:sectPr>
            </w:body>
            </w:document>
            """);
        return sb.ToString();
    }

    private static void AppendQuestion(StringBuilder sb, QuestionBlock question, int level)
    {
        var marks = string.IsNullOrWhiteSpace(question.Marks) ? string.Empty : $" ({question.Marks} marks)";
        var cloLine = FormatCloLine(question.Clos);
        var number = string.IsNullOrWhiteSpace(question.Number) ? string.Empty : question.Number.Trim();
        var heading = string.IsNullOrWhiteSpace(number)
            ? question.Text
            : level == 0
                ? $"{number}{marks}"
                : $"{number}) {question.Text}{marks}";

        if (level == 0)
        {
            sb.Append(Paragraph(heading, bold: true, size: 24, after: string.IsNullOrWhiteSpace(cloLine) ? 80 : 40));
            if (!string.IsNullOrWhiteSpace(cloLine))
            {
                sb.Append(Paragraph(cloLine, bold: true, size: 22, after: 80));
            }

            foreach (var line in SplitLines(question.Text))
            {
                sb.Append(Paragraph(line, bold: false, size: 24, after: 80, indent: 0));
            }
        }
        else
        {
            sb.Append(Paragraph(heading, bold: false, size: 24, after: string.IsNullOrWhiteSpace(cloLine) ? 80 : 40, indent: 240));
            if (!string.IsNullOrWhiteSpace(cloLine))
            {
                sb.Append(Paragraph(cloLine, bold: true, size: 22, after: 80, indent: 240));
            }
        }

        foreach (var part in question.Parts)
        {
            AppendQuestion(sb, part, level + 1);
        }
    }

    private static string FormatCloLine(string? clos)
    {
        var value = (clos ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value) || value == "—")
        {
            return string.Empty;
        }

        return $"CLO: {value}";
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        var value = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var line in value.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static string CleanTitle(string title, int assignmentNo, string kind)
    {
        var value = (title ?? string.Empty).Trim();
        var paper = string.IsNullOrWhiteSpace(kind) ? "Assignment" : Regex.Escape(kind.Trim());
        value = Regex.Replace(
            value,
            $@"^(Assignment|Quiz|{paper})\s*{assignmentNo}\b",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();
        value = Regex.Replace(value, @"^[^A-Za-z0-9]+", string.Empty).Trim();
        return value;
    }

    private static string Paragraph(string text, bool bold, int size, int after, int indent = 0)
    {
        var extra = bold ? "<w:b/><w:bCs/>" : "";
        var ind = indent > 0 ? $"<w:ind w:left=\"{indent}\"/>" : "";
        return $"""
            <w:p>
              <w:pPr>
                {ind}
                <w:spacing w:after="{after}" w:line="240" w:lineRule="auto"/>
                <w:rPr>
                  <w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>
                  {extra}<w:sz w:val="{size}"/><w:szCs w:val="{size}"/>
                </w:rPr>
              </w:pPr>
              <w:r>
                <w:rPr>
                  <w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>
                  {extra}<w:sz w:val="{size}"/><w:szCs w:val="{size}"/>
                </w:rPr>
                <w:t xml:space="preserve">{Esc(text)}</w:t>
              </w:r>
            </w:p>
            """;
    }

    private static string Esc(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
        </Types>
        """;

    private const string RelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
        """;
}

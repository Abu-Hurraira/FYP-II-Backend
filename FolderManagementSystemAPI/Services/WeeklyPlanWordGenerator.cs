using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderManagementSystemAPI.Services;

public static class WeeklyPlanWordGenerator
{
    public sealed record OutlineLine(int Level, string Text, bool Bold);

    public sealed record WeekRow(string WeekNo, IReadOnlyList<OutlineLine> Lectures, IReadOnlyList<OutlineLine> Lab);

    public static byte[] Generate(
        string courseTitle,
        string courseCode,
        string sessionLabel,
        IReadOnlyList<WeekRow> weeks)
    {
        var documentXml = BuildDocumentXml(courseTitle, courseCode, sessionLabel, weeks);
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

    public static bool IsLabTitle(string? title) =>
        Regex.IsMatch((title ?? string.Empty).Trim(), @"^lab(\b|:|-)", RegexOptions.IgnoreCase);

    private static void WriteEntry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string BuildDocumentXml(
        string courseTitle,
        string courseCode,
        string sessionLabel,
        IReadOnlyList<WeekRow> weeks)
    {
        var heading = string.IsNullOrWhiteSpace(courseCode)
            ? courseTitle
            : $"{courseTitle} ({courseCode})";
        var outline = string.IsNullOrWhiteSpace(sessionLabel)
            ? "Course Outline"
            : $"Course Outline ({sessionLabel})";

        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>
            """);
        sb.Append(Paragraph(heading, bold: true, size: 32, align: "center", after: 40));
        sb.Append(Paragraph(outline, bold: false, size: 20, align: "center", after: 200, italic: true));
        sb.Append(StartTable());
        sb.Append("<w:tr>");
        sb.Append(HeaderCell("Week #", 900));
        sb.Append(HeaderCell("List of Topics (Lectures)", 5600));
        sb.Append(HeaderCell("Lab", 3580));
        sb.Append("</w:tr>");

        foreach (var week in weeks)
        {
            sb.Append("<w:tr>");
            sb.Append(Cell(week.WeekNo, 900, bold: true, align: "center", vAlign: "center"));
            sb.Append(BulletCell(week.Lectures, 5600));
            sb.Append(BulletCell(week.Lab, 3580));
            sb.Append("</w:tr>");
        }

        sb.Append("</w:tbl>");
        sb.Append("""
            <w:sectPr>
              <w:pgSz w:w="12240" w:h="15840"/>
              <w:pgMar w:top="720" w:right="720" w:bottom="720" w:left="720"/>
            </w:sectPr>
            </w:body>
            </w:document>
            """);
        return sb.ToString();
    }

    private static string StartTable() =>
        """
        <w:tbl>
          <w:tblPr>
            <w:tblW w:w="5000" w:type="pct"/>
            <w:tblBorders>
              <w:top w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              <w:left w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              <w:bottom w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              <w:right w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              <w:insideH w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              <w:insideV w:val="single" w:sz="8" w:space="0" w:color="000000"/>
            </w:tblBorders>
          </w:tblPr>
        """;

    private static string HeaderCell(string text, int width) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:shd w:val="clear" w:color="auto" w:fill="D9D9D9"/>
            <w:vAlign w:val="center"/>
          </w:tcPr>
          <w:p>
            <w:pPr><w:jc w:val="center"/></w:pPr>
            <w:r>
              <w:rPr><w:b/><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr>
              <w:t xml:space="preserve">{Esc(text)}</w:t>
            </w:r>
          </w:p>
        </w:tc>
        """;

    private static string Cell(string text, int width, bool bold, string align, string vAlign) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:vAlign w:val="{vAlign}"/>
          </w:tcPr>
          <w:p>
            <w:pPr><w:jc w:val="{align}"/></w:pPr>
            <w:r>
              <w:rPr>{(bold ? "<w:b/>" : "")}<w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr>
              <w:t xml:space="preserve">{Esc(text)}</w:t>
            </w:r>
          </w:p>
        </w:tc>
        """;

    private static string BulletCell(IReadOnlyList<OutlineLine> lines, int width)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="{width}" w:type="dxa"/>
                <w:vAlign w:val="top"/>
              </w:tcPr>
            """);
        if (lines.Count == 0)
        {
            sb.Append("<w:p></w:p>");
        }
        else
        {
            foreach (var line in lines)
            {
                var indent = 80 + line.Level * 220;
                var mark = line.Level == 0 ? "– " : "o ";
                var bold = line.Bold ? "<w:b/>" : "";
                sb.Append($"""
                    <w:p>
                      <w:pPr>
                        <w:ind w:left="{indent}"/>
                        <w:spacing w:after="40"/>
                      </w:pPr>
                      <w:r>
                        <w:rPr>{bold}<w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
                        <w:t xml:space="preserve">{Esc(mark + line.Text)}</w:t>
                      </w:r>
                    </w:p>
                    """);
            }
        }

        sb.Append("</w:tc>");
        return sb.ToString();
    }

    private static string Paragraph(
        string text,
        bool bold,
        int size,
        string align,
        int after,
        bool italic = false)
    {
        var extra = (bold ? "<w:b/>" : "") + (italic ? "<w:i/>" : "");
        return $"""
            <w:p>
              <w:pPr>
                <w:jc w:val="{align}"/>
                <w:spacing w:after="{after}"/>
              </w:pPr>
              <w:r>
                <w:rPr>{extra}<w:sz w:val="{size}"/><w:szCs w:val="{size}"/></w:rPr>
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

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderManagementSystemAPI.Services;

public static class CloGaMappingWordGenerator
{
    public sealed record ProgramOutcome(string Letter, string Description);

    public sealed record CloRow(string Number, string Description, IReadOnlySet<string> MappedLetters);

    public static byte[] Generate(
        string programName,
        string courseTitle,
        string courseCode,
        IReadOnlyList<ProgramOutcome> outcomes,
        IReadOnlyList<CloRow> clos)
    {
        var documentXml = BuildDocumentXml(programName, courseTitle, courseCode, outcomes, clos);
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

    public static string LetterForPlo(string? ploCode, int index)
    {
        var code = (ploCode ?? string.Empty).Trim();
        if (code.Length == 1 && char.IsLetter(code[0]))
        {
            return char.ToLowerInvariant(code[0]).ToString();
        }

        var match = Regex.Match(code, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var number) && number is >= 1 and <= 26)
        {
            return ((char)('a' + number - 1)).ToString();
        }

        return ((char)('a' + Math.Clamp(index, 0, 25))).ToString();
    }

    public static string NumberForClo(string? cloCode, int index)
    {
        var match = Regex.Match(cloCode ?? string.Empty, @"\d+");
        if (match.Success)
        {
            return match.Value;
        }

        return (index + 1).ToString();
    }

    private static void WriteEntry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string BuildDocumentXml(
        string programName,
        string courseTitle,
        string courseCode,
        IReadOnlyList<ProgramOutcome> outcomes,
        IReadOnlyList<CloRow> clos)
    {
        var letters = outcomes.Select(o => o.Letter).ToList();
        var courseLabel = string.IsNullOrWhiteSpace(courseCode)
            ? courseTitle
            : $"{courseCode} {courseTitle}".Trim();
        var programHeading = string.IsNullOrWhiteSpace(programName)
            ? "Program Outcomes"
            : $"{programName.Trim()} Program Outcomes";

        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>
            """);
        sb.Append(CenteredParagraph("Course Learning Outcomes", bold: true, size: 32));
        sb.Append(CenteredParagraph($"Course: {courseLabel}", bold: true, size: 24));
        sb.Append(Paragraph(programHeading, bold: true, size: 22, before: 240));

        sb.Append(StartTable());
        foreach (var outcome in outcomes)
        {
            sb.Append("<w:tr>");
            sb.Append(Cell(outcome.Letter, width: 720, bold: true, align: "center"));
            sb.Append(Cell(outcome.Description, width: 9360, align: "left"));
            sb.Append("</w:tr>");
        }
        sb.Append("</w:tbl>");

        sb.Append("<w:p><w:pPr><w:spacing w:before=\"280\"/></w:pPr></w:p>");
        sb.Append(StartTable());

        sb.Append("<w:tr>");
        sb.Append(Cell(courseLabel, width: 4200, bold: true, align: "left", gridSpan: 2, vMerge: "restart"));
        sb.Append(Cell("Program Learning Outcomes", width: 5880, bold: true, align: "center", gridSpan: Math.Max(letters.Count, 1)));
        sb.Append("</w:tr>");

        sb.Append("<w:tr>");
        sb.Append(Cell("", width: 600, vMerge: "continue"));
        sb.Append(Cell("", width: 3600, vMerge: "continue"));
        foreach (var letter in letters)
        {
            sb.Append(Cell(letter, width: 588, bold: true, align: "center"));
        }
        sb.Append("</w:tr>");

        foreach (var clo in clos)
        {
            sb.Append("<w:tr>");
            sb.Append(Cell(clo.Number, width: 600, bold: true, align: "center"));
            sb.Append(Cell(clo.Description, width: 3600, bold: true, align: "left"));
            foreach (var letter in letters)
            {
                var marked = clo.MappedLetters.Contains(letter);
                sb.Append(Cell(marked ? "X" : "", width: 588, bold: true, align: "center"));
            }
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

    private static string CenteredParagraph(string text, bool bold, int size) =>
        Paragraph(text, bold, size, align: "center", before: 80, after: 80);

    private static string Paragraph(
        string text,
        bool bold,
        int size,
        string align = "left",
        int before = 0,
        int after = 80)
    {
        var b = bold ? "<w:b/>" : "";
        return $"""
            <w:p>
              <w:pPr>
                <w:jc w:val="{align}"/>
                <w:spacing w:before="{before}" w:after="{after}"/>
              </w:pPr>
              <w:r>
                <w:rPr>{b}<w:sz w:val="{size}"/><w:szCs w:val="{size}"/></w:rPr>
                <w:t xml:space="preserve">{Esc(text)}</w:t>
              </w:r>
            </w:p>
            """;
    }

    private static string Cell(
        string text,
        int width,
        bool bold = false,
        string align = "left",
        int gridSpan = 1,
        string? vMerge = null)
    {
        var span = gridSpan > 1 ? $"<w:gridSpan w:val=\"{gridSpan}\"/>" : "";
        var merge = string.IsNullOrWhiteSpace(vMerge) ? "" : $"<w:vMerge w:val=\"{vMerge}\"/>";
        var b = bold ? "<w:b/>" : "";
        return $"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="{width}" w:type="dxa"/>
                {span}{merge}
                <w:vAlign w:val="center"/>
              </w:tcPr>
              <w:p>
                <w:pPr><w:jc w:val="{align}"/></w:pPr>
                <w:r>
                  <w:rPr>{b}<w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
                  <w:t xml:space="preserve">{Esc(text)}</w:t>
                </w:r>
              </w:p>
            </w:tc>
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

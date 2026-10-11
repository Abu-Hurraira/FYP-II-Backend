using System.IO.Compression;
using System.Net;
using System.Text;

namespace FolderManagementSystemAPI.Services;

public static class MarksDistributionWordGenerator
{
    public sealed record InstrumentRow(string Label, string Weight);

    public sealed record DetailSection(
        string Heading,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string>> Rows);

    public static byte[] Generate(
        string subtitle,
        IReadOnlyList<InstrumentRow> instruments,
        IReadOnlyList<string> gradingScaleLines,
        IReadOnlyList<DetailSection> detailSections)
    {
        var documentXml = BuildDocumentXml(subtitle, instruments, gradingScaleLines, detailSections);
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

    private static void WriteEntry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string BuildDocumentXml(
        string subtitle,
        IReadOnlyList<InstrumentRow> instruments,
        IReadOnlyList<string> gradingScaleLines,
        IReadOnlyList<DetailSection> detailSections)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>
            """);
        sb.Append(Paragraph("Marks Distribution and Grading", bold: true, size: 28, align: "center", after: 60));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            sb.Append(Paragraph(subtitle, bold: false, size: 18, align: "center", after: 200, italic: true));
        }

        sb.Append(BuildInstrumentsAndGradingTable(instruments, gradingScaleLines));

        foreach (var section in detailSections)
        {
            sb.Append(Paragraph(section.Heading, bold: true, size: 22, align: "left", after: 80));
            sb.Append(BuildSimpleTable(section.Headers, section.Rows));
            sb.Append(Paragraph(string.Empty, bold: false, size: 18, align: "left", after: 120));
        }

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

    private static string BuildInstrumentsAndGradingTable(
        IReadOnlyList<InstrumentRow> instruments,
        IReadOnlyList<string> gradingScaleLines)
    {
        const int col1 = 3200;
        const int col2 = 4200;
        const int col3 = 2680;
        var sb = new StringBuilder();
        sb.Append(StartTable());

        for (var i = 0; i < instruments.Count; i++)
        {
            var row = instruments[i];
            sb.Append("<w:tr>");
            if (i == 0)
            {
                sb.Append(MergedLabelCell(
                    "Assessment Instruments with Weights (homework, quizzes, midterms, final, programming assignments, lab work, etc.)",
                    col1,
                    restart: true));
            }
            else
            {
                sb.Append(MergedLabelCell(string.Empty, col1, restart: false));
            }

            sb.Append(TextCell(row.Label, col2, bold: false, align: "left"));
            sb.Append(TextCell(row.Weight, col3, bold: true, align: "center"));
            sb.Append("</w:tr>");
        }

        sb.Append("<w:tr>");
        sb.Append(TextCell("Grading Model", col1, bold: true, align: "left", vAlign: "center"));
        sb.Append(TextCell("As per University Policy", col2, bold: false, align: "left", vAlign: "center"));
        sb.Append(MultiLineCell("Grading Scale:", gradingScaleLines, col3));
        sb.Append("</w:tr>");

        sb.Append("</w:tbl>");
        sb.Append(Paragraph(string.Empty, bold: false, size: 18, align: "left", after: 200));
        return sb.ToString();
    }

    private static string BuildSimpleTable(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var colCount = Math.Max(headers.Count, 1);
        var width = 10080 / colCount;
        var sb = new StringBuilder();
        sb.Append(StartTable());
        sb.Append("<w:tr>");
        foreach (var header in headers)
        {
            sb.Append(HeaderCell(header, width));
        }

        sb.Append("</w:tr>");

        foreach (var row in rows)
        {
            sb.Append("<w:tr>");
            for (var i = 0; i < colCount; i++)
            {
                var text = i < row.Count ? row[i] ?? string.Empty : string.Empty;
                var align = i == 0 ? "left" : "center";
                sb.Append(TextCell(text, width, bold: false, align: align));
            }

            sb.Append("</w:tr>");
        }

        sb.Append("</w:tbl>");
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

    private static string MergedLabelCell(string text, int width, bool restart) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:vMerge w:val="{(restart ? "restart" : "continue")}"/>
            <w:vAlign w:val="center"/>
          </w:tcPr>
          <w:p>
            <w:r>
              <w:rPr><w:b/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
              <w:t xml:space="preserve">{Esc(text)}</w:t>
            </w:r>
          </w:p>
        </w:tc>
        """;

    private static string TextCell(
        string text,
        int width,
        bool bold,
        string align,
        string vAlign = "center") =>
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

    private static string MultiLineCell(string heading, IReadOnlyList<string> lines, int width)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="{width}" w:type="dxa"/>
                <w:vAlign w:val="top"/>
              </w:tcPr>
              <w:p>
                <w:pPr><w:spacing w:after="40"/></w:pPr>
                <w:r>
                  <w:rPr><w:b/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
                  <w:t xml:space="preserve">{Esc(heading)}</w:t>
                </w:r>
              </w:p>
            """);
        foreach (var line in lines)
        {
            sb.Append($"""
                <w:p>
                  <w:pPr><w:spacing w:after="20"/></w:pPr>
                  <w:r>
                    <w:rPr><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
                    <w:t xml:space="preserve">{Esc(line)}</w:t>
                  </w:r>
                </w:p>
                """);
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

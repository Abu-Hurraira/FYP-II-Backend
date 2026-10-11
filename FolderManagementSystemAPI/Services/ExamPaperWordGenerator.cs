using System.IO.Compression;
using System.Net;
using System.Text;

namespace FolderManagementSystemAPI.Services;

public static class ExamPaperWordGenerator
{
    public sealed record ExamQuestion(
        string Number,
        string Text,
        string Clo,
        string Marks,
        IReadOnlyList<ExamQuestion> Parts);

    public sealed record ExamHeader(
        string CourseTitle,
        string CourseCode,
        string SessionLine,
        string ExamDate,
        string Duration,
        string DegreeProgram,
        string TotalMarks,
        string TeacherName);

    public static byte[] Generate(
        ExamHeader header,
        IReadOnlyList<ExamQuestion> questions,
        byte[]? leftLogo,
        byte[]? rightLogo)
    {
        var hasLeft = leftLogo is { Length: > 0 };
        var hasRight = rightLogo is { Length: > 0 };
        var documentXml = BuildDocumentXml(header, questions, hasLeft, hasRight);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteText(zip, "[Content_Types].xml", ContentTypesXml);
            WriteText(zip, "_rels/.rels", RelsXml);
            WriteText(zip, "word/_rels/document.xml.rels", DocumentRelsXml);
            WriteText(zip, "word/document.xml", documentXml);
            WriteText(zip, "word/footer1.xml", FooterXml);
            if (hasLeft)
            {
                WriteBinary(zip, "word/media/image1.png", leftLogo!);
            }

            if (hasRight)
            {
                WriteBinary(zip, "word/media/image2.jpeg", rightLogo!);
            }
        }

        return stream.ToArray();
    }

    private static void WriteText(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static void WriteBinary(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string BuildDocumentXml(
        ExamHeader header,
        IReadOnlyList<ExamQuestion> questions,
        bool hasLeft,
        bool hasRight)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
              xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
              xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
              xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
              xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
            <w:body>
            """);
        sb.Append(Paragraph(
            "Student's Name: ____________________    Reg #: ____________    Class/Section: ____________",
            bold: false,
            size: 20,
            align: "center",
            after: 120,
            italic: true));

        sb.Append(StartTable(10080, bordered: false));
        sb.Append("<w:tr>");
        sb.Append(LogoCell(hasLeft ? ImageDrawing(1, "rIdImage1", 822960) : "", 1400));
        sb.Append(UniversityCell(header.SessionLine, 7280));
        sb.Append(LogoCell(hasRight ? ImageDrawing(2, "rIdImage2", 822960) : "", 1400));
        sb.Append("</w:tr></w:tbl>");

        sb.Append(StartTable(10080));
        sb.Append(MetaRow("Course Title:", header.CourseTitle, "Course Code:", header.CourseCode));
        sb.Append(MetaRow("Date of Exam:", header.ExamDate, "Time & Duration:", header.Duration));
        sb.Append(MetaRow("Degree Program:", header.DegreeProgram, "Total Marks:", header.TotalMarks));
        sb.Append("<w:tr>");
        sb.Append(BorderedCell("Teacher Name:", 2200, bold: true));
        sb.Append(BorderedCell(header.TeacherName, 7880, underline: true, gridSpan: 3));
        sb.Append("</w:tr></w:tbl>");

        sb.Append(StartTable(10080));
        sb.Append("<w:tr>");
        sb.Append(HeaderCell("Q. No", 900));
        sb.Append(HeaderCell("Question", 6500));
        sb.Append(HeaderCell("CLO", 1400));
        sb.Append(HeaderCell("Marks", 1280));
        sb.Append("</w:tr>");

        if (questions.Count == 0)
        {
            sb.Append("<w:tr>");
            sb.Append(BorderedCell("No questions have been added yet.", 10080, gridSpan: 4));
            sb.Append("</w:tr>");
        }
        else
        {
            foreach (var question in questions)
            {
                sb.Append("<w:tr>");
                sb.Append(BorderedCell(question.Number, 900, bold: true, align: "center", vAlign: "top"));
                sb.Append(QuestionCell(question, 6500));
                sb.Append(BorderedCell(question.Clo, 1400, align: "center", vAlign: "top"));
                sb.Append(BorderedCell(question.Marks, 1280, align: "center", vAlign: "top"));
                sb.Append("</w:tr>");
            }
        }

        sb.Append("</w:tbl>");
        sb.Append("""
            <w:sectPr>
              <w:footerReference w:type="default" r:id="rIdFooter"/>
              <w:pgSz w:w="12240" w:h="15840"/>
              <w:pgMar w:top="720" w:right="720" w:bottom="860" w:left="720"/>
            </w:sectPr>
            </w:body>
            </w:document>
            """);
        return sb.ToString();
    }

    private static string StartTable(int width, bool bordered = true)
    {
        var borders = bordered
            ? """
              <w:tblBorders>
                <w:top w:val="single" w:sz="8" w:space="0" w:color="000000"/>
                <w:left w:val="single" w:sz="8" w:space="0" w:color="000000"/>
                <w:bottom w:val="single" w:sz="8" w:space="0" w:color="000000"/>
                <w:right w:val="single" w:sz="8" w:space="0" w:color="000000"/>
                <w:insideH w:val="single" w:sz="8" w:space="0" w:color="000000"/>
                <w:insideV w:val="single" w:sz="8" w:space="0" w:color="000000"/>
              </w:tblBorders>
              """
            : """
              <w:tblBorders>
                <w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/>
                <w:insideH w:val="nil"/><w:insideV w:val="nil"/>
              </w:tblBorders>
              """;
        return $"""
            <w:tbl>
              <w:tblPr>
                <w:tblW w:w="{width}" w:type="dxa"/>
                {borders}
              </w:tblPr>
            """;
    }

    private static string LogoCell(string drawingXml, int width) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:vAlign w:val="center"/>
            <w:tcBorders>
              <w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/>
            </w:tcBorders>
          </w:tcPr>
          <w:p>
            <w:pPr><w:jc w:val="center"/></w:pPr>
            {(string.IsNullOrEmpty(drawingXml) ? "" : $"<w:r>{drawingXml}</w:r>")}
          </w:p>
        </w:tc>
        """;

    private static string UniversityCell(string sessionLine, int width) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:vAlign w:val="center"/>
            <w:tcBorders>
              <w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/>
            </w:tcBorders>
          </w:tcPr>
          {Paragraph("Barani Institute of Information Technology", true, 26, "center", 40)}
          {Paragraph("PMAS Arid Agriculture University", true, 22, "center", 20)}
          {Paragraph("Rawalpindi, Pakistan", false, 20, "center", 80)}
          {Paragraph(sessionLine, true, 22, "center", 40)}
        </w:tc>
        """;

    private static string MetaRow(string l1, string v1, string l2, string v2)
    {
        return "<w:tr>"
            + BorderedCell(l1, 2200, bold: true)
            + BorderedCell(v1, 3200, underline: true)
            + BorderedCell(l2, 2200, bold: true)
            + BorderedCell(v2, 2480, underline: true)
            + "</w:tr>";
    }

    private static string HeaderCell(string text, int width) =>
        $"""
        <w:tc>
          <w:tcPr>
            <w:tcW w:w="{width}" w:type="dxa"/>
            <w:shd w:val="clear" w:color="auto" w:fill="D9D9D9"/>
            <w:vAlign w:val="center"/>
          </w:tcPr>
          {Paragraph(text, true, 20, "center", 40)}
        </w:tc>
        """;

    private static string BorderedCell(
        string text,
        int width,
        bool bold = false,
        bool underline = false,
        string align = "left",
        string vAlign = "center",
        int gridSpan = 1)
    {
        var span = gridSpan > 1 ? $"<w:gridSpan w:val=\"{gridSpan}\"/>" : "";
        return $"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="{width}" w:type="dxa"/>
                {span}
                <w:vAlign w:val="{vAlign}"/>
              </w:tcPr>
              {Paragraph(text, bold, 20, align, 40, underline: underline)}
            </w:tc>
            """;
    }

    private static string QuestionCell(ExamQuestion question, int width)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="{width}" w:type="dxa"/>
                <w:vAlign w:val="top"/>
              </w:tcPr>
            """);
        foreach (var line in SplitLines(question.Text))
        {
            sb.Append(Paragraph(line, false, 20, "left", 40));
        }

        if (question.Parts.Count > 0)
        {
            sb.Append(Paragraph("Instructions:", true, 20, "left", 40));
            var index = 1;
            foreach (var part in question.Parts)
            {
                sb.Append(Paragraph($"{index}. {part.Text}", false, 20, "left", 40));
                foreach (var nested in part.Parts)
                {
                    sb.Append(Paragraph($"o  {nested.Text}", false, 20, "left", 40, indent: 240));
                }

                index++;
            }
        }

        if (question.Parts.Count == 0 && SplitLines(question.Text).Count() == 0)
        {
            sb.Append("<w:p></w:p>");
        }

        sb.Append("</w:tc>");
        return sb.ToString();
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

    private static string ImageDrawing(int id, string relId, int emu)
    {
        return $"""
            <w:drawing>
              <wp:inline distT="0" distB="0" distL="0" distR="0">
                <wp:extent cx="{emu}" cy="{emu}"/>
                <wp:docPr id="{id}" name="Picture {id}"/>
                <a:graphic>
                  <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                    <pic:pic>
                      <pic:nvPicPr>
                        <pic:cNvPr id="0" name="image{id}"/>
                        <pic:cNvPicPr/>
                      </pic:nvPicPr>
                      <pic:blipFill>
                        <a:blip r:embed="{relId}"/>
                        <a:stretch><a:fillRect/></a:stretch>
                      </pic:blipFill>
                      <pic:spPr>
                        <a:xfrm>
                          <a:off x="0" y="0"/>
                          <a:ext cx="{emu}" cy="{emu}"/>
                        </a:xfrm>
                        <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                      </pic:spPr>
                    </pic:pic>
                  </a:graphicData>
                </a:graphic>
              </wp:inline>
            </w:drawing>
            """;
    }

    private static string Paragraph(
        string text,
        bool bold,
        int size,
        string align,
        int after,
        bool italic = false,
        bool underline = false,
        int indent = 0)
    {
        var extra = (bold ? "<w:b/><w:bCs/>" : "")
            + (italic ? "<w:i/>" : "")
            + (underline ? "<w:u w:val=\"single\"/>" : "");
        var ind = indent > 0 ? $"<w:ind w:left=\"{indent}\"/>" : "";
        return $"""
            <w:p>
              <w:pPr>
                {ind}
                <w:jc w:val="{align}"/>
                <w:spacing w:after="{after}"/>
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
          <Default Extension="png" ContentType="image/png"/>
          <Default Extension="jpeg" ContentType="image/jpeg"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
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
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rIdFooter" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
          <Relationship Id="rIdImage1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
          <Relationship Id="rIdImage2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image2.jpeg"/>
        </Relationships>
        """;

    private const string FooterXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:p>
            <w:pPr><w:jc w:val="right"/></w:pPr>
            <w:r>
              <w:rPr>
                <w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman"/>
                <w:sz w:val="18"/>
              </w:rPr>
              <w:t xml:space="preserve">Page </w:t>
            </w:r>
            <w:r><w:fldChar w:fldCharType="begin"/></w:r>
            <w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
            <w:r><w:fldChar w:fldCharType="end"/></w:r>
            <w:r>
              <w:rPr>
                <w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman"/>
                <w:sz w:val="18"/>
              </w:rPr>
              <w:t xml:space="preserve"> of </w:t>
            </w:r>
            <w:r><w:fldChar w:fldCharType="begin"/></w:r>
            <w:r><w:instrText xml:space="preserve"> NUMPAGES </w:instrText></w:r>
            <w:r><w:fldChar w:fldCharType="end"/></w:r>
          </w:p>
        </w:ftr>
        """;
}

using FolderManagementSystemAPI.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FolderManagementSystemAPI.Services;

public static class FcarPdfGenerator
{
    private const string HeaderColor = "#FFF39A";
    private const string SuccessColor = "#CDECCF";

    static FcarPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(FcarReportDto report)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(42);
                page.MarginTop(30);
                page.MarginBottom(18);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(6.5f));

                page.Header().Column(header =>
                {
                    header.Item().AlignCenter().Text("Faculty Course Assessment Report").Bold().FontSize(10);
                    header.Item().AlignCenter().Text($"Course: {report.CourseTitle}").Bold().FontSize(8);
                });

                page.Content().PaddingTop(6).Column(column =>
                {
                    column.Spacing(6);
                    ComposeProgramOutcomes(column, report);
                    ComposeCloPloMatrix(column, report);
                    ComposeMetricGoals(column, report);
                    ComposeCourseDetails(column, report);
                    ComposeGradeDistribution(column, report);
                    ComposeModifications(column, report);
                    ComposeCloAssessments(column, report);
                    ComposeEpan(column, report);
                    ComposeNarratives(column, report);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("FCAR  •  Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeProgramOutcomes(ColumnDescriptor column, FcarReportDto report)
    {
        var program = string.IsNullOrWhiteSpace(report.ProgramName)
            ? "Program Outcomes"
            : $"{report.ProgramName} Program Outcomes";
        column.Item().Text(program).Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);
                c.RelativeColumn();
            });
            foreach (var plo in report.ProgramOutcomes)
            {
                table.Cell().Element(HeaderCell).Text(plo.PloCode);
                table.Cell().Element(BodyCell).Text(plo.PloDescription);
            }
        });
    }

    private static void ComposeCloPloMatrix(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Text("Course Learning Outcomes × Program Learning Outcomes").Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(6);
                foreach (var _ in report.ProgramOutcomes) c.ConstantColumn(22);
            });
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("CLO × PLO");
                foreach (var plo in report.ProgramOutcomes)
                    header.Cell().Element(HeaderCell).AlignCenter().Text(plo.PloCode);
            });
            foreach (var clo in report.CloPloMappings)
            {
                table.Cell().Element(HeaderCell).Text($"{clo.CloCode}: {clo.CloDescription}");
                foreach (var plo in report.ProgramOutcomes)
                {
                    var mapped = clo.MappedPloCodes.Contains(plo.PloCode, StringComparer.OrdinalIgnoreCase);
                    table.Cell().Element(BodyCell).AlignCenter().Text(mapped ? "x" : string.Empty);
                }
            }
        });
    }

    private static void ComposeMetricGoals(ColumnDescriptor column, FcarReportDto report)
    {
        if (report.MappedPloCodes.Count == 0) return;
        column.Item().Text("Metric Goal").Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                foreach (var _ in report.MappedPloCodes) c.RelativeColumn();
            });
            table.Cell().Element(HeaderCell).Text("PLO");
            foreach (var code in report.MappedPloCodes)
                table.Cell().Element(HeaderCell).AlignCenter().Text(code);
            table.Cell().Element(BodyCell).Text("Goal");
            foreach (var _ in report.MappedPloCodes)
                table.Cell().Element(BodyCell).AlignCenter().Text(report.DefaultMetricGoal.ToString("0.0"));
        });
    }

    private static void ComposeCourseDetails(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });
            DetailRow(table, "Course Code & Number", report.CourseCode);
            DetailRow(table, "Sections", report.SectionName);
            DetailRow(table, "Total Credits", report.TotalCreditHours.ToString());
            DetailRow(table, "Instructor", report.TeacherName);
            DetailRow(table, "Course Title", report.CourseTitle);
            DetailRow(table, "Academic Term", report.SessionTitle);
            DetailRow(table, "Current Catalog Description", report.CatalogDescription);
        });
    }

    private static void ComposeGradeDistribution(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Text("Grade Distribution (*F Grades are excluded for calculating EPAN and EEMU)").Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
            });
            foreach (var grade in new[] { "A", "B", "C", "D" })
                table.Cell().Element(HeaderCell).AlignCenter().Text(grade);
            foreach (var count in new[]
                     {
                         report.GradeDistribution.GradeACount,
                         report.GradeDistribution.GradeBCount,
                         report.GradeDistribution.GradeCCount,
                         report.GradeDistribution.GradeDCount
                     })
                table.Cell().Element(BodyCell).AlignCenter().Text(count.ToString());
        });
    }

    private static void ComposeModifications(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });
            DetailRow(table, "Modifications Made to Course", report.CourseModifications);
        });
    }

    private static void ComposeCloAssessments(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Text("Course Learning Outcomes — Measurement (EEMU)").Bold().FontSize(8);
        foreach (var clo in report.CloAssessments)
        {
            column.Item().Background(HeaderColor).Border(0.5f).Padding(3)
                .Text($"{clo.CloCode}. {clo.CloDescription}").Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.ConstantColumn(25);
                    c.ConstantColumn(25);
                    c.ConstantColumn(25);
                    c.ConstantColumn(25);
                    c.RelativeColumn(2);
                    c.ConstantColumn(38);
                });
                table.Header(header =>
                {
                    foreach (var heading in new[] { "Head Wise Grades", "A", "B", "C", "D", "EEMU vector", "Avg." })
                        header.Cell().Element(HeaderCell).AlignCenter().Text(heading);
                });
                foreach (var item in clo.Items)
                {
                    table.Cell().Element(BodyCell).Text(item.ItemName);
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.GradeACount.ToString());
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.GradeBCount.ToString());
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.GradeCCount.ToString());
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.GradeDCount.ToString());
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.EemuVector);
                    table.Cell().Element(BodyCell).AlignCenter().Text(item.AverageScore.ToString("0.###"));
                }
                table.Cell().ColumnSpan(6).Element(HeaderCell).AlignRight().Text("Avg.");
                table.Cell().Element(HeaderCell).AlignCenter().Text(clo.OverallCloAverage.ToString("0.###"));
            });
        }
    }

    private static void ComposeEpan(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Text("EPAN Roll-up").Bold().FontSize(8);
        if (report.MappedPloCodes.Count == 0)
        {
            column.Item().Text("No approved CLO to PLO mappings are available.");
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                foreach (var _ in report.MappedPloCodes) c.RelativeColumn();
            });
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text(report.CourseTitle);
                foreach (var code in report.MappedPloCodes)
                    header.Cell().Element(HeaderCell).AlignCenter().Text(code);
            });

            foreach (var clo in report.CloAssessments)
            {
                var mapping = report.CloPloMappings.FirstOrDefault(x => x.CloId == clo.CloId);
                table.Cell().Element(HeaderCell).Text($"{clo.CloCode}. {clo.CloDescription}");
                foreach (var code in report.MappedPloCodes)
                {
                    var mapped = mapping?.MappedPloCodes.Contains(code, StringComparer.OrdinalIgnoreCase) == true;
                    table.Cell().Element(BodyCell).AlignCenter()
                        .Text(mapped ? clo.OverallCloAverage.ToString("0.###") : string.Empty);
                }
            }

            table.Cell().Element(SuccessCell).Text("EPAN (Average)").Bold();
            foreach (var code in report.MappedPloCodes)
            {
                var epan = report.PloEpans.FirstOrDefault(x =>
                    string.Equals(x.PloCode, code, StringComparison.OrdinalIgnoreCase));
                table.Cell().Element(SuccessCell).AlignCenter().Text((epan?.EpanAverage ?? 0).ToString("0.###")).Bold();
            }
            table.Cell().Element(BodyCell).Text("Metric Goal");
            foreach (var code in report.MappedPloCodes)
            {
                var epan = report.PloEpans.FirstOrDefault(x =>
                    string.Equals(x.PloCode, code, StringComparison.OrdinalIgnoreCase));
                table.Cell().Element(BodyCell).AlignCenter().Text((epan?.MetricGoal ?? 2m).ToString("0.0"));
            }
            table.Cell().Element(SuccessCell).Text("Proficiency").Bold();
            foreach (var code in report.MappedPloCodes)
            {
                var epan = report.PloEpans.FirstOrDefault(x =>
                    string.Equals(x.PloCode, code, StringComparison.OrdinalIgnoreCase));
                table.Cell().Element(SuccessCell).AlignCenter().Text(epan?.Status ?? "DEVELOPING").Bold();
            }
        });
    }

    private static void ComposeNarratives(ColumnDescriptor column, FcarReportDto report)
    {
        column.Item().Text("Narratives").Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(4);
            });
            DetailRow(table, "Program Outcome Assessment", Dash(report.ProgramOutcomeAssessmentText));
            DetailRow(table, "Student Feedback", Dash(report.StudentFeedbackText));
            DetailRow(table, "Reflection", Dash(report.ReflectionText));
            DetailRow(table, "Proposed Actions for Course Improvement", Dash(report.ProposedActionsText));
        });
    }

    private static void DetailRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(HeaderCell).Text(label).Bold();
        table.Cell().Element(BodyCell).Text(value);
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "–" : value;
    private static IContainer HeaderCell(IContainer container) =>
        container.Background(HeaderColor).Border(0.5f).Padding(2);
    private static IContainer BodyCell(IContainer container) =>
        container.Border(0.5f).Padding(2);
    private static IContainer SuccessCell(IContainer container) =>
        container.Background(SuccessColor).Border(0.5f).Padding(2);
}

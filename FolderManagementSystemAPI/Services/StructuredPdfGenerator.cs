using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FolderManagementSystemAPI.Services;

public static class StructuredPdfGenerator
{
    private const string HeaderColor = "#E8F5E9";
    private const string TitleColor = "#1B5E20";

    static StructuredPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GenerateSections(
        string title,
        string? subtitle,
        IReadOnlyList<(string Heading, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> sections)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text(title).Bold().FontSize(16).FontColor(TitleColor);
                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        header.Item().PaddingTop(2).Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
                    }
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(14);
                    if (sections.Count == 0)
                    {
                        column.Item().Text("No content available yet.");
                        return;
                    }

                    foreach (var section in sections)
                    {
                        column.Item().Text(section.Heading).Bold().FontSize(12).FontColor(TitleColor);
                        if (section.Rows.Count == 0)
                        {
                            column.Item().Text("No data saved yet.");
                            continue;
                        }

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columnsDef =>
                            {
                                foreach (var _ in section.Headers)
                                {
                                    columnsDef.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var heading in section.Headers)
                                {
                                    header.Cell().Element(HeaderCell).Text(heading).Bold();
                                }
                            });

                            foreach (var row in section.Rows)
                            {
                                for (var i = 0; i < section.Headers.Count; i++)
                                {
                                    var value = i < row.Count ? row[i] : string.Empty;
                                    table.Cell().Element(BodyCell).Text(value);
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateTable(
        string title,
        string? subtitle,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text(title).Bold().FontSize(16).FontColor(TitleColor);
                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        header.Item().PaddingTop(2).Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
                    }
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    if (rows.Count == 0)
                    {
                        column.Item().Text("No content available yet.");
                        return;
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in headers)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var heading in headers)
                            {
                                header.Cell().Element(HeaderCell).Text(heading).Bold();
                            }
                        });

                        foreach (var row in rows)
                        {
                            for (var i = 0; i < headers.Count; i++)
                            {
                                var value = i < row.Count ? row[i] : string.Empty;
                                table.Cell().Element(BodyCell).Text(value);
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateQuestionPaper(
        string title,
        IReadOnlyList<(string Label, string Value)> meta,
        IReadOnlyList<(string Number, string Marks, string Clos, string Text)> questions)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().AlignCenter().Text(title).Bold().FontSize(14).FontColor(TitleColor);
                });

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(8);

                    if (meta.Count > 0)
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(4);
                            });
                            foreach (var (label, value) in meta)
                            {
                                table.Cell().Element(HeaderCell).Text(label).Bold();
                                table.Cell().Element(BodyCell).Text(value);
                            }
                        });
                    }

                    column.Item().Text("Questions").Bold().FontSize(12);

                    if (questions.Count == 0)
                    {
                        column.Item().Text("No questions have been added yet.");
                        return;
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(48);
                            c.ConstantColumn(50);
                            c.RelativeColumn(2);
                            c.RelativeColumn(6);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Q #").Bold();
                            header.Cell().Element(HeaderCell).Text("Marks").Bold();
                            header.Cell().Element(HeaderCell).Text("CLO").Bold();
                            header.Cell().Element(HeaderCell).Text("Question").Bold();
                        });

                        foreach (var question in questions)
                        {
                            table.Cell().Element(BodyCell).Text(question.Number);
                            table.Cell().Element(BodyCell).Text(question.Marks);
                            table.Cell().Element(BodyCell).Text(question.Clos);
                            table.Cell().Element(BodyCell).Text(question.Text);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(HeaderColor).Border(0.5f).Padding(4);

    private static IContainer BodyCell(IContainer container) =>
        container.Border(0.5f).Padding(4);
}

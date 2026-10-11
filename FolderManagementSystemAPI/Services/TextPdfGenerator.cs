using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FolderManagementSystemAPI.Services;

public static class TextPdfGenerator
{
    static TextPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(string title, string content)
    {
        var body = string.IsNullOrWhiteSpace(content)
            ? "No content available yet."
            : content;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(11).LineHeight(1.4f));

                page.Header()
                    .PaddingBottom(12)
                    .Text(title)
                    .FontSize(18)
                    .Bold()
                    .FontColor(Colors.Green.Darken3);

                page.Content()
                    .Text(body);
            });
        }).GeneratePdf();
    }
}

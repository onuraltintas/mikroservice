using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpeedReading.Application.Reports;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace SpeedReading.Infrastructure.Exports;

public sealed class ReportExportService : ISpeedReadingReportExporter
{
    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(ReportExportRequest request)
    {
        var rows = SpeedReadingReportExportRules.Flatten(request.Data);
        var title = SpeedReadingReportExportRules.ResolveTitle(request);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(10).FontFamily("Arial"));
                page.Header().Element(content => ComposeHeader(content, request, title));
                page.Content().Element(content => ComposeContent(content, rows));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateExcel(ReportExportRequest request)
    {
        var rows = SpeedReadingReportExportRules.Flatten(request.Data);
        var title = SpeedReadingReportExportRules.ResolveTitle(request);

        using var stream = new MemoryStream();
        using (var spreadsheetDocument = SpreadsheetDocument.Create(
                   stream,
                   SpreadsheetDocumentType.Workbook,
                   true))
        {
            var workbookPart = spreadsheetDocument.AddWorkbookPart();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new Spreadsheet.SheetData();
            worksheetPart.Worksheet = new Spreadsheet.Worksheet(sheetData);

            var sheets = workbookPart.Workbook = new Spreadsheet.Workbook();
            sheets.AppendChild(new Spreadsheet.Sheets(
                new Spreadsheet.Sheet
                {
                    Name = "Rapor",
                    SheetId = 1U,
                    Id = workbookPart.GetIdOfPart(worksheetPart)
                }));

            AppendRow(sheetData, title);
            AppendRow(sheetData, "Rapor türü", request.ReportType ?? "");
            AppendRow(sheetData, "Başlangıç", request.StartDate?.ToString("O") ?? "");
            AppendRow(sheetData, "Bitiş", request.EndDate?.ToString("O") ?? "");
            AppendRow(sheetData);
            AppendRow(sheetData, "Alan", "Değer");

            foreach (var row in rows)
            {
                AppendRow(sheetData, row.Field, row.Value);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void ComposeHeader(
        IContainer container,
        ReportExportRequest request,
        string title)
    {
        container.Column(column =>
        {
            column.Item().Text(title)
                .FontSize(20)
                .SemiBold()
                .FontColor(Colors.Blue.Darken3);

            if (!string.IsNullOrWhiteSpace(request.ReportType))
            {
                column.Item().PaddingTop(4).Text($"Tür: {request.ReportType}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            }

            if (request.StartDate.HasValue || request.EndDate.HasValue)
            {
                column.Item().PaddingTop(3).Text(
                        $"Tarih aralığı: {FormatDate(request.StartDate)} - {FormatDate(request.EndDate)}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            }

            column.Item().PaddingTop(15).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, IReadOnlyList<ReportExportRow> rows)
    {
        container.PaddingVertical(12).Column(column =>
        {
            if (rows.Count == 0)
            {
                column.Item().Text("Rapor verisi bulunamadı.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().PaddingBottom(6).Row(row =>
            {
                row.ConstantItem(180).Text("Alan").SemiBold();
                row.RelativeItem().Text("Değer").SemiBold();
            });

            foreach (var item in rows)
            {
                column.Item().PaddingVertical(3).Row(row =>
                {
                    row.ConstantItem(180).Text(item.Field).SemiBold();
                    row.RelativeItem().Text(item.Value);
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text("Master Hızlı Okuma Platformu")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Sayfa ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
    }

    private static void AppendRow(Spreadsheet.SheetData sheetData, params string[] values)
    {
        var row = new Spreadsheet.Row();
        foreach (var value in values)
        {
            row.AppendChild(new Spreadsheet.Cell
            {
                DataType = Spreadsheet.CellValues.InlineString,
                InlineString = new Spreadsheet.InlineString(
                    new Spreadsheet.Text(value) { Space = SpaceProcessingModeValues.Preserve })
            });
        }

        sheetData.AppendChild(row);
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
}

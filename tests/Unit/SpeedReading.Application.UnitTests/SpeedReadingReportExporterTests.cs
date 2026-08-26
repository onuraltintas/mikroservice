using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.Reports;
using SpeedReading.Infrastructure.Exports;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingReportExporterTests
{
    [Fact]
    public void GeneratePdf_returns_a_readable_pdf_with_report_content()
    {
        using var document = JsonDocument.Parse("""
            { "reportType": "student-detail", "title": "Öğrenci Raporu", "data": { "score": 82 } }
            """);

        var bytes = new ReportExportService().GeneratePdf(
            SpeedReadingReportExportRules.Normalize(document.RootElement));

        bytes[..5].Should().Equal(Encoding.ASCII.GetBytes("%PDF-"));
        bytes.Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void GenerateExcel_returns_openxml_with_report_headers_and_content()
    {
        using var document = JsonDocument.Parse("""
            { "reportType": "student-detail", "title": "Öğrenci Raporu", "data": { "score": 82 } }
            """);

        var bytes = new ReportExportService().GenerateExcel(
            SpeedReadingReportExportRules.Normalize(document.RootElement));

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        worksheet.Should().NotBeNull();
        using var reader = new StreamReader(worksheet!.Open());
        var xml = reader.ReadToEnd();
        xml.Should().Contain("Öğrenci Raporu");
        xml.Should().Contain("score");
        xml.Should().Contain("82");
    }
}

using System.IO.Compression;
using System.Text;
using FluentAssertions;
using SpeedReading.Application.Content;
using SpeedReading.Infrastructure.Exports;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingReadingTextExportTests
{
    [Fact]
    public void GeneratePdf_ReturnsPdfDocument()
    {
        var bytes = new ReadingTextExportService().GeneratePdf(CreateText());

        bytes[..5].Should().Equal(Encoding.ASCII.GetBytes("%PDF-"));
    }

    [Fact]
    public void GenerateDocx_ReturnsOpenXmlDocumentWithQuestions()
    {
        var bytes = new ReadingTextExportService().GenerateDocx(CreateText());

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var document = archive.GetEntry("word/document.xml");
        document.Should().NotBeNull();
        using var reader = new StreamReader(document!.Open());
        var xml = reader.ReadToEnd();
        xml.Should().Contain("Test metni");
        xml.Should().Contain("SORULAR");
        xml.Should().Contain("CEVAP ANAHTARI");
    }

    [Fact]
    public void GenerateMultipleDocx_IncludesEveryText()
    {
        var first = CreateText("Birinci");
        var second = CreateText("İkinci");

        var bytes = new ReadingTextExportService().GenerateMultipleDocx([first, second]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();
        xml.Should().Contain("Birinci");
        xml.Should().Contain("İkinci");
    }

    private static ReadingTextDetails CreateText(string title = "Test metni") =>
        new(
            Guid.NewGuid(),
            title,
            "Bu, dışa aktarma testi için örnek bir okuma metnidir.",
            9,
            "Genel",
            2,
            null,
            "tr",
            true,
            [],
            null,
            [new ReadingQuestionSummary(
                Guid.NewGuid(),
                "Soru nedir?",
                1,
                1,
                1,
                null,
                "Doğru",
                "Yanlış",
                "Diğer",
                "Son",
                "A",
                0)],
            1,
            10);
}

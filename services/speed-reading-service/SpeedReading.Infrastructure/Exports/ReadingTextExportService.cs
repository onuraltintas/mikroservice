using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Exports;

public sealed class ReadingTextExportService : ISpeedReadingReadingTextExporter
{
    static ReadingTextExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(ReadingTextDetails text) => GenerateMultiplePdf([text]);

    public byte[] GenerateMultiplePdf(IReadOnlyList<ReadingTextDetails> texts)
    {
        EnsureTexts(texts);

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            foreach (var text in texts)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Arial"));
                    page.Header().Element(content => ComposeHeader(content, text));
                    page.Content().Element(content => ComposeContent(content, text));
                    page.Footer().Element(ComposeFooter);
                });
            }
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateDocx(ReadingTextDetails text) => GenerateMultipleDocx([text]);

    public byte[] GenerateMultipleDocx(IReadOnlyList<ReadingTextDetails> texts)
    {
        EnsureTexts(texts);

        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            for (var index = 0; index < texts.Count; index++)
            {
                if (index > 0)
                {
                    AddPageBreak(body);
                }

                AddText(body, texts[index]);
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void ComposeHeader(IContainer container, ReadingTextDetails text)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(header =>
                {
                    header.Item().Text("OKUMA METNİ")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1)
                        .SemiBold();
                    header.Item().Text(text.Title)
                        .FontSize(20)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken3);
                    header.Item().PaddingTop(5).Text(
                        $"{text.Category} | Seviye {text.DifficultyLevel} | {text.WordCount} kelime | ~{Math.Ceiling(text.WordCount / 200.0)} dk")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingTop(15).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, ReadingTextDetails text)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Item().PaddingBottom(20).Text(text.Content)
                .FontSize(11)
                .LineHeight(1.6f)
                .Justify();

            if (text.Questions.Count == 0)
            {
                return;
            }

            column.Item().PageBreak();
            column.Item().Text("SORULAR")
                .FontSize(14)
                .SemiBold()
                .FontColor(Colors.Blue.Darken3);

            var questions = text.Questions.OrderBy(question => question.OrderIndex).ToList();
            for (var index = 0; index < questions.Count; index++)
            {
                var question = questions[index];
                column.Item().PaddingTop(12).Column(questionColumn =>
                {
                    questionColumn.Item().Text($"{index + 1}. {question.QuestionText}")
                        .FontSize(11)
                        .SemiBold();
                    questionColumn.Item().PaddingTop(5).PaddingLeft(15).Column(options =>
                    {
                        ComposeOption(options, "A", question.OptionA, question.CorrectAnswer);
                        ComposeOption(options, "B", question.OptionB, question.CorrectAnswer);
                        ComposeOption(options, "C", question.OptionC, question.CorrectAnswer);
                        ComposeOption(options, "D", question.OptionD, question.CorrectAnswer);
                    });
                });
            }

            column.Item().PaddingTop(25).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                .PaddingTop(10).Text("CEVAP ANAHTARI")
                .FontSize(12)
                .SemiBold()
                .FontColor(Colors.Green.Darken3);
            column.Item().PaddingTop(5).Text(string.Join(", ", questions.Select((question, index) =>
                    $"{index + 1}-{question.CorrectAnswer ?? "-"}")))
                .FontSize(10)
                .FontColor(Colors.Green.Darken2);
        });
    }

    private static void ComposeOption(ColumnDescriptor column, string letter, string optionText, string? correctAnswer)
    {
        var isCorrect = letter.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase);
        column.Item().PaddingVertical(2).Row(row =>
        {
            row.AutoItem().Width(20).Text($"{letter})")
                .FontSize(10)
                .FontColor(isCorrect ? Colors.Green.Darken3 : Colors.Grey.Darken1);
            row.RelativeItem().Text(optionText)
                .FontSize(10)
                .FontColor(isCorrect ? Colors.Green.Darken3 : Colors.Black);
            if (isCorrect)
            {
                row.AutoItem().PaddingLeft(5).Text("✓")
                    .FontSize(10)
                    .FontColor(Colors.Green.Darken3)
                    .SemiBold();
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

    private static void AddText(Body body, ReadingTextDetails text)
    {
        AddParagraph(body, "OKUMA METNİ", 10, false, "808080");
        AddParagraph(body, text.Title, 24, true, "1E3A5F");
        AddParagraph(
            body,
            $"{text.Category} | Seviye {text.DifficultyLevel} | {text.WordCount} kelime | ~{Math.Ceiling(text.WordCount / 200.0)} dk",
            9,
            false,
            "808080");
        AddParagraph(body, string.Empty, 11, false);
        AddParagraph(body, text.Content, 11, false);
        AddParagraph(body, string.Empty, 11, false);

        if (text.Questions.Count == 0)
        {
            return;
        }

        AddPageBreak(body);
        AddParagraph(body, "SORULAR", 14, true, "1E3A5F");
        AddParagraph(body, string.Empty, 11, false);

        var questions = text.Questions.OrderBy(question => question.OrderIndex).ToList();
        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            AddParagraph(body, $"{index + 1}. {question.QuestionText}", 11, true);
            AddOptionParagraph(body, "A", question.OptionA, question.CorrectAnswer);
            AddOptionParagraph(body, "B", question.OptionB, question.CorrectAnswer);
            AddOptionParagraph(body, "C", question.OptionC, question.CorrectAnswer);
            AddOptionParagraph(body, "D", question.OptionD, question.CorrectAnswer);
            AddParagraph(body, string.Empty, 11, false);
        }

        AddParagraph(body, "CEVAP ANAHTARI", 12, true, "228B22");
        AddParagraph(
            body,
            string.Join(", ", questions.Select((question, index) => $"{index + 1}-{question.CorrectAnswer ?? "-"}")),
            10,
            false,
            "228B22");
    }

    private static void AddParagraph(Body body, string text, int fontSize, bool bold, string? colorHex = null)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        var properties = new RunProperties
        {
            FontSize = new FontSize { Val = (fontSize * 2).ToString() }
        };
        if (bold)
        {
            properties.AppendChild(new Bold());
        }

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            properties.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = colorHex });
        }

        run.AppendChild(properties);
        run.AppendChild(new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
        paragraph.AppendChild(run);
        body.AppendChild(paragraph);
    }

    private static void AddOptionParagraph(Body body, string letter, string optionText, string? correctAnswer)
    {
        var isCorrect = letter.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase);
        AddParagraph(
            body,
            isCorrect ? $"    {letter}) {optionText} ✓" : $"    {letter}) {optionText}",
            10,
            isCorrect,
            isCorrect ? "228B22" : "000000");
    }

    private static void AddPageBreak(Body body) => body.AppendChild(
        new Paragraph(new Run(new Break { Type = BreakValues.Page })));

    private static void EnsureTexts(IReadOnlyList<ReadingTextDetails> texts)
    {
        if (texts.Count == 0)
        {
            throw new ArgumentException("At least one reading text is required.", nameof(texts));
        }
    }
}

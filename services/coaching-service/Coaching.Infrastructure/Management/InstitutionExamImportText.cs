using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Roadmap O4 follow-up: institution exam results come as CSV text or as an Excel (.xlsx) workbook with the same columns.
/// The workbook's first sheet becomes semicolon-separated text whose line numbers are the Excel row numbers.
/// </summary>
public static class InstitutionExamImportText
{
    public const int MaxWorkbookBytes = 1_500_000;
    private const int MaxRows = 5_001;
    private const int MaxColumns = 200;

    public static string From(string? csv, string? xlsxBase64)
    {
        if (!string.IsNullOrWhiteSpace(xlsxBase64))
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(xlsxBase64); }
            catch (FormatException) { throw Unreadable(); }
            return FirstSheet(bytes);
        }
        return string.IsNullOrWhiteSpace(csv) ? throw new ArgumentException("Bir CSV ya da Excel dosyası seçin.") : csv;
    }

    public static string FirstSheet(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaxWorkbookBytes) throw new ArgumentException("Excel dosyası en fazla 1,5 MB olabilir.");
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbook = document.WorkbookPart ?? throw Unreadable();
            var sheet = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw Unreadable();
            if (sheet.Id?.Value is not { } id || workbook.GetPartById(id) is not WorksheetPart part) throw Unreadable();
            var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(x => x.InnerText).ToArray() ?? [];
            var lines = new List<string>();
            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                var index = (int)(row.RowIndex?.Value ?? (uint)(lines.Count + 1));
                if (index > MaxRows) throw new ArgumentException($"Dosya en fazla {MaxRows - 1} öğrenci satırı içerebilir.");
                while (lines.Count < index - 1) lines.Add(string.Empty);
                var cells = new SortedDictionary<int, string>();
                var next = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = cell.CellReference?.Value is { } reference ? Column(reference) : next;
                    if (column >= MaxColumns) throw new ArgumentException($"Dosya en fazla {MaxColumns} sütun içerebilir.");
                    cells[column] = Clean(Value(cell, shared)); next = column + 1;
                }
                lines.Add(cells.Count == 0 ? string.Empty : string.Join(";", Enumerable.Range(0, cells.Keys.Max() + 1).Select(x => cells.GetValueOrDefault(x, string.Empty))).TrimEnd(';'));
            }
            return string.Join("\n", lines);
        }
        catch (ArgumentException) { throw; }
        catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or FileFormatException or IOException
            or InvalidOperationException or KeyNotFoundException or System.Xml.XmlException)
        { throw Unreadable(); }
    }

    private static string Value(Cell cell, IReadOnlyList<string> shared)
    {
        var raw = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
            return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < shared.Count ? shared[index] : string.Empty;
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.Boolean) return raw == "1" ? "1" : "0";
        // Numbers are stored invariantly (412.5); the importer accepts both 412.5 and 412,5.
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number.ToString(CultureInfo.InvariantCulture) : raw;
    }

    private static int Column(string reference)
    {
        var column = 0;
        foreach (var letter in reference.TakeWhile(char.IsLetter)) column = column * 26 + (char.ToUpperInvariant(letter) - 'A' + 1);
        return Math.Max(0, column - 1);
    }

    private static string Clean(string value) => value.Replace(';', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static ArgumentException Unreadable() => new("Excel dosyası okunamadı. Dosyayı .xlsx olarak kaydedip yeniden deneyin.");
}

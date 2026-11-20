using Microsoft.VisualBasic.FileIO;

namespace CourseManagement.Application;

public sealed record StudentCsvImportRow(int RowNumber, string DisplayName, string? Email, string? PhoneNumber, string? Error)
{
    public bool IsValid => Error is null;
}

public sealed record StudentCsvImportDocument(IReadOnlyList<StudentCsvImportRow> Rows);

public sealed class StudentCsvImportParseException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class StudentCsvImportParser
{
    public const int MaximumCsvLength = 1_000_000;
    public const int MaximumRows = 500;

    public static StudentCsvImportDocument Parse(string? csv)
    {
        if (string.IsNullOrEmpty(csv)) throw new StudentCsvImportParseException("StudentImport.EmptyFile");
        if (csv.Length > MaximumCsvLength) throw new StudentCsvImportParseException("StudentImport.FileTooLarge");

        var text = csv.TrimStart('\uFEFF');
        using var reader = new StringReader(text);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(Delimiter(text));

        string[]? headers;
        try
        {
            headers = parser.ReadFields();
        }
        catch (MalformedLineException)
        {
            throw new StudentCsvImportParseException("StudentImport.MalformedCsv");
        }

        if (headers is null || headers.Length == 0)
            throw new StudentCsvImportParseException("StudentImport.InvalidHeader");

        var normalizedHeaders = headers.Select(Column).ToArray();
        var known = normalizedHeaders.Where(header => header is not null).ToArray();
        if (known.Distinct(StringComparer.Ordinal).Count() != known.Length || !known.Contains(DisplayName, StringComparer.Ordinal))
            throw new StudentCsvImportParseException("StudentImport.InvalidHeader");

        var displayNameIndex = Array.IndexOf(normalizedHeaders, DisplayName);
        var emailIndex = Array.IndexOf(normalizedHeaders, Email);
        var phoneIndex = Array.IndexOf(normalizedHeaders, Phone);
        var rows = new List<StudentCsvImportRow>();
        while (!parser.EndOfData)
        {
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                throw new StudentCsvImportParseException("StudentImport.MalformedCsv");
            }

            if (fields is null || fields.All(string.IsNullOrWhiteSpace)) continue;
            if (rows.Count == MaximumRows) throw new StudentCsvImportParseException("StudentImport.TooManyRows");

            var rowNumber = rows.Count + 2;
            if (fields.Length != normalizedHeaders.Length)
            {
                rows.Add(new StudentCsvImportRow(rowNumber, string.Empty, null, null, "Satırdaki sütun sayısı başlıkla eşleşmiyor."));
                continue;
            }

            var displayName = fields[displayNameIndex].Trim();
            var email = Optional(fields, emailIndex)?.ToLowerInvariant();
            var phoneNumber = Optional(fields, phoneIndex);
            var error = displayName.Length is < 1 or > 200 ? "Öğrenci adı 1–200 karakter olmalıdır."
                : email?.Length > 320 ? "E-posta en fazla 320 karakter olabilir."
                : phoneNumber?.Length > 40 ? "Telefon en fazla 40 karakter olabilir."
                : null;
            rows.Add(new StudentCsvImportRow(rowNumber, displayName, email, phoneNumber, error));
        }

        return new StudentCsvImportDocument(rows);
    }

    private const string DisplayName = "displayname";
    private const string Email = "email";
    private const string Phone = "phonenumber";

    /// <summary>
    /// Header names the panel tells people to write ("ad, e-posta, telefon") and the English API names. Other columns
    /// (a parent's name, a class) are left out, so an Excel list can be sent as it is (20.11.2026 live test).
    /// </summary>
    private static readonly Dictionary<string, string> Columns = new(StringComparer.Ordinal)
    {
        ["displayname"] = DisplayName, ["name"] = DisplayName, ["ad"] = DisplayName, ["adi"] = DisplayName, ["adsoyad"] = DisplayName,
        ["adisoyadi"] = DisplayName, ["adsoyadi"] = DisplayName, ["isim"] = DisplayName, ["isimsoyisim"] = DisplayName,
        ["ogrenci"] = DisplayName, ["ogrenciadi"] = DisplayName, ["ogrenciadisoyadi"] = DisplayName,
        ["email"] = Email, ["eposta"] = Email, ["mail"] = Email, ["epostaadresi"] = Email,
        ["phonenumber"] = Phone, ["phone"] = Phone, ["telefon"] = Phone, ["tel"] = Phone, ["telefonnumarasi"] = Phone,
        ["gsm"] = Phone, ["cep"] = Phone, ["ceptelefonu"] = Phone
    };

    /// <summary>The column a header names, or null for a column the import does not read.</summary>
    private static string? Column(string header)
    {
        var folded = new string(header.Trim().ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))
            .Select(letter => letter switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', _ => letter })
            .Where(char.IsLetterOrDigit).ToArray());
        return Columns.GetValueOrDefault(folded);
    }

    /// <summary>Excel in Turkish saves CSV with semicolons; the first line shows which separator the file uses.</summary>
    private static string Delimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var first = end < 0 ? text : text[..end];
        var semicolons = first.Count(letter => letter == ';');
        var tabs = first.Count(letter => letter == '\t');
        var commas = first.Count(letter => letter == ',');
        return semicolons > commas && semicolons >= tabs ? ";" : tabs > commas ? "\t" : ",";
    }

    private static string? Optional(string[] fields, int index)
    {
        if (index < 0 || string.IsNullOrWhiteSpace(fields[index])) return null;
        return fields[index].Trim();
    }
}

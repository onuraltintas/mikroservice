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

        using var reader = new StringReader(csv.TrimStart('\uFEFF'));
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

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

        var normalizedHeaders = headers.Select(header => header.Trim().ToLowerInvariant()).ToArray();
        if (normalizedHeaders.Any(string.IsNullOrWhiteSpace)
            || normalizedHeaders.Distinct(StringComparer.Ordinal).Count() != normalizedHeaders.Length
            || normalizedHeaders.Any(header => header is not ("displayname" or "email" or "phonenumber"))
            || !normalizedHeaders.Contains("displayname", StringComparer.Ordinal))
            throw new StudentCsvImportParseException("StudentImport.InvalidHeader");

        var displayNameIndex = Array.IndexOf(normalizedHeaders, "displayname");
        var emailIndex = Array.IndexOf(normalizedHeaders, "email");
        var phoneIndex = Array.IndexOf(normalizedHeaders, "phonenumber");
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

    private static string? Optional(string[] fields, int index)
    {
        if (index < 0 || string.IsNullOrWhiteSpace(fields[index])) return null;
        return fields[index].Trim();
    }
}

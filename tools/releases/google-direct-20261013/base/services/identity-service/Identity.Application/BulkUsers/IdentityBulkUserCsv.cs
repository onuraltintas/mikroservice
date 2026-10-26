using System.Globalization;
using System.Text;
using Identity.Application.Queries.GetUserProfile;

namespace Identity.Application.BulkUsers;

public sealed record BulkUserCsvRow(
    int RowNumber,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string Role);

public sealed record BulkUserCsvParseResult(
    IReadOnlyList<BulkUserCsvRow> Rows,
    IReadOnlyList<string> Errors);

public sealed record BulkUserCsvExportRow(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    bool EmailConfirmed,
    DateTime? CreatedAt,
    DateTime? LastLoginAt);

public static class IdentityBulkUserCsv
{
    public const int ColumnCount = 5;
    public const string Header = "firstName,lastName,email,phoneNumber,role";

    private static readonly string[] ExpectedHeaders =
        ["firstname", "lastname", "email", "phonenumber", "role"];

    public static BulkUserCsvParseResult Parse(string content, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        List<List<string>> records;
        try
        {
            records = ParseRecords(content);
        }
        catch (FormatException exception)
        {
            return new([], [exception.Message]);
        }
        if (records.Count == 0)
            return new([], ["CSV dosyası boş."]);

        var headers = records[0]
            .Select(NormalizeHeader)
            .ToArray();
        if (!headers.SequenceEqual(ExpectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            return new([], [$"CSV başlıkları şu sırada olmalıdır: {Header}"]);
        }

        var rows = new List<BulkUserCsvRow>();
        var errors = new List<string>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataRecords = records.Skip(1).Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value))).ToList();

        if (dataRecords.Count > maxRows)
            errors.Add($"CSV en fazla {maxRows} kullanıcı içerebilir.");

        foreach (var (record, index) in dataRecords.Take(maxRows).Select((record, index) => (record, index)))
        {
            var rowNumber = index + 2;
            if (record.Count != ColumnCount)
            {
                errors.Add($"Satır {rowNumber}: {ColumnCount} sütun bekleniyor.");
                continue;
            }

            var firstName = record[0].Trim();
            var lastName = record[1].Trim();
            var email = record[2].Trim();
            var phoneNumber = string.IsNullOrWhiteSpace(record[3]) ? null : record[3].Trim();
            var role = record[4].Trim();
            var rowErrors = ValidateRow(firstName, lastName, email, phoneNumber, role);

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors.Select(error => $"Satır {rowNumber}: {error}"));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add($"Satır {rowNumber}: E-posta bu dosyada tekrar ediyor.");
                continue;
            }

            rows.Add(new BulkUserCsvRow(rowNumber, firstName, lastName, email, phoneNumber, role));
        }

        return new(rows, errors);
    }

    public static string Export(IEnumerable<BulkUserCsvExportRow> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        var builder = new StringBuilder();
        builder.AppendLine("id,email,firstName,lastName,phoneNumber,roles,isActive,emailConfirmed,createdAt,lastLoginAt");
        foreach (var user in users)
        {
            AppendRecord(builder,
            [
                user.UserId.ToString("D"),
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber ?? string.Empty,
                string.Join('|', user.Roles),
                user.IsActive.ToString(CultureInfo.InvariantCulture),
                user.EmailConfirmed.ToString(CultureInfo.InvariantCulture),
                user.CreatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                user.LastLoginAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            ]);
        }

        return builder.ToString();
    }

    public static BulkUserCsvExportRow ToExportRow(UserProfileDto user) =>
        new(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Roles,
            user.IsActive,
            user.EmailConfirmed,
            user.CreatedAt,
            user.LastLoginAt);

    private static List<string> ValidateRow(
        string firstName,
        string lastName,
        string email,
        string? phoneNumber,
        string role)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100)
            errors.Add("Ad zorunludur ve 100 karakteri geçemez.");
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
            errors.Add("Soyad zorunludur ve 100 karakteri geçemez.");
        if (!IsValidEmail(email))
            errors.Add("Geçerli bir e-posta adresi girilmelidir.");
        if (phoneNumber is not null && phoneNumber.Length > 50)
            errors.Add("Telefon numarası 50 karakteri geçemez.");
        if (string.IsNullOrWhiteSpace(role) || role.Length > 50)
            errors.Add("Rol zorunludur ve 50 karakteri geçemez.");
        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255)
            return false;

        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    currentField.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                currentRecord.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            if (character == '\n' && !inQuotes)
            {
                currentRecord.Add(currentField.ToString().TrimEnd('\r'));
                currentField.Clear();
                records.Add(currentRecord);
                currentRecord = new List<string>();
                continue;
            }

            currentField.Append(character);
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            if (inQuotes)
                throw new FormatException("CSV içinde kapanmamış tırnak bulundu.");

            currentRecord.Add(currentField.ToString().TrimEnd('\r'));
            records.Add(currentRecord);
        }

        if (inQuotes)
            throw new FormatException("CSV içinde kapanmamış tırnak bulundu.");

        return records;
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().TrimStart('\uFEFF').Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static void AppendRecord(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append(Escape(fields[index]));
        }
        builder.AppendLine();
    }

    private static string Escape(string value)
    {
        var safeValue = value.Length > 0 && "=+-@".Contains(value[0], StringComparison.Ordinal)
            ? $"'{value}"
            : value;
        return safeValue.Contains(',', StringComparison.Ordinal)
            || safeValue.Contains('"', StringComparison.Ordinal)
            || safeValue.Contains('\n', StringComparison.Ordinal)
            || safeValue.Contains('\r', StringComparison.Ordinal)
            ? $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : safeValue;
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Billing.Domain;

public enum BankTransactionStatus
{
    /// <summary>No order could be tied to it by itself; an admin looks at it.</summary>
    Unmatched = 0,
    /// <summary>Its code and amount fit a waiting order, and the order's product is approved by hand.</summary>
    Matched = 1,
    /// <summary>Used by an approved payment; cannot be used again.</summary>
    Approved = 2,
    /// <summary>Not a sale (set by an admin).</summary>
    Ignored = 3
}

/// <summary>One upload of a bank statement for one bank account.</summary>
public sealed class BankStatementImport
{
    public Guid Id { get; set; }
    public Guid BankAccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public int RowCount { get; set; }
    public int AddedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int MatchedCount { get; set; }
    public int AutoApprovedCount { get; set; }
}

/// <summary>An incoming line of a bank statement. The sender's name is personal data and is kept with the financial records.</summary>
public sealed class BankTransaction
{
    public Guid Id { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid ImportId { get; set; }
    public DateTime BookedAt { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Sender { get; set; }
    public string? Reference { get; set; }
    /// <summary>The payment code read from the description, if any.</summary>
    public string? PaymentCode { get; set; }
    /// <summary>The same line uploaded again is recognised by this and not added twice.</summary>
    public string Fingerprint { get; set; } = string.Empty;
    public BankTransactionStatus Status { get; set; }
    /// <summary>The order its code points to, or the one an admin tied it to.</summary>
    public Guid? OrderId { get; set; }
    public Guid? PaymentId { get; set; }
    /// <summary>Why it was not matched by itself, or the admin's note.</summary>
    public string? Note { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Which columns of the uploaded table hold what; zero-based. The header row itself is skipped. Some banks (Halkbank)
/// write every amount without a sign and say in another column whether the money came in ("A", alacak) or went out
/// ("B", borç): then <see cref="DirectionColumn"/> and <see cref="IncomingValue"/> tell the incoming lines apart.
/// </summary>
public sealed record StatementMapping(int HeaderRow, int DateColumn, int DescriptionColumn, int AmountColumn, int? SenderColumn, int? ReferenceColumn,
    int? DirectionColumn = null, string? IncomingValue = null);

public sealed record StatementLine(DateTime BookedAt, decimal Amount, string Description, string? Sender, string? Reference);

/// <summary>Reading a bank statement: finding the columns, dates, amounts and the payment code (stage 3).</summary>
public static partial class StatementRules
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRows = 5_000;
    public const int PreviewRows = 40;
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);
    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "d/M/yyyy",
        "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "dd-MM-yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss"
    ];

    /// <summary>
    /// The payment code in a transfer description. People type it with a space, without the dash or in lower case:
    /// "edu 7k4q2m", "EDU7K4Q2M". Letters that are not in the code alphabet never match.
    /// </summary>
    public static string? FindPaymentCode(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var match = PaymentCode().Match(description.ToUpperInvariant());
        return match.Success ? BillingRules.PaymentCodePrefix + match.Groups[1].Value : null;
    }

    /// <summary>"1.234,56", "1,234.56", "1234.56", "200", "+200,00 TL". Null when it is not a number.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = new string(text.Where(character => char.IsDigit(character) || character is ',' or '.' or '-' or '+').ToArray());
        if (value.Length == 0 || !value.Any(char.IsDigit)) return null;
        var negative = value.StartsWith('-') || value.EndsWith('-');
        value = value.Trim('-', '+');
        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');
        char? decimalSeparator;
        if (lastComma >= 0 && lastDot >= 0) decimalSeparator = lastComma > lastDot ? ',' : '.';
        // In a Turkish statement a single comma is the decimal comma: "200,5", "1234,56".
        else if (lastComma >= 0) decimalSeparator = value.Count(character => character == ',') == 1 ? ',' : null;
        // A single dot followed by exactly three digits is a thousands separator in Turkish statements: "1.234".
        else if (lastDot >= 0) decimalSeparator = value.Count(character => character == '.') == 1 && value.Length - lastDot - 1 != 3 ? '.' : null;
        else decimalSeparator = null;
        var digits = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsDigit(character)) digits.Append(character);
            else if (character == decimalSeparator && index == (decimalSeparator == ',' ? lastComma : lastDot)) digits.Append('.');
        }
        return decimal.TryParse(digits.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? Math.Round(negative ? -amount : amount, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    /// <summary>A statement date in Turkey time, as UTC. Excel serial numbers are read too. Null when it is not a date.</summary>
    public static DateTime? ParseDate(string? text)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        DateTime local;
        if (DateTime.TryParseExact(value, DateFormats, Turkish, DateTimeStyles.None, out var parsed)) local = parsed;
        else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20_000 and < 80_000) local = DateTime.FromOADate(serial);
        else return null;
        return DateTime.SpecifyKind(local - TurkeyOffset, DateTimeKind.Utc);
    }

    /// <summary>The header row and columns guessed from the usual Turkish column names; null when they are not found.</summary>
    public static StatementMapping? SuggestMapping(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, PreviewRows); rowIndex++)
        {
            var cells = rows[rowIndex].Select(cell => cell.Trim().ToLower(Turkish)).ToArray();
            int Find(params string[] names) => Array.FindIndex(cells, cell => names.Any(name => cell.Contains(name, StringComparison.Ordinal)));
            int? Optional(params string[] names) => Find(names) is var index and >= 0 ? index : null;
            var date = Find("tarih");
            var description = Find("açıklama", "aciklama");
            // "Alacak" is the incoming column where a statement splits money in and out; otherwise the signed amount.
            var amount = Find("alacak") is var credit and >= 0 ? credit : Find("tutar", "miktar");
            if (date >= 0 && description >= 0 && amount >= 0)
            {
                // Halkbank: "B/A" holds A for money in and B for money out; "BİM REF" is the line's own reference.
                var direction = Array.FindIndex(cells, cell => cell is "b/a" or "borç/alacak" or "borc/alacak");
                return new StatementMapping(rowIndex, date, description, amount, Optional("gönderen", "gonderen", "karşı hesap", "karsi hesap"),
                    Optional("bim ref", "dekont", "referans", "fiş no", "fis no", "işlem no", "islem no"),
                    direction >= 0 ? direction : null, direction >= 0 ? "A" : null);
            }
        }
        return null;
    }

    /// <summary>The incoming lines of the table under the header row. Lines without a date or with no money coming in are skipped.</summary>
    public static IReadOnlyList<StatementLine> ReadLines(IReadOnlyList<IReadOnlyList<string>> rows, StatementMapping mapping)
    {
        var lines = new List<StatementLine>();
        for (var rowIndex = mapping.HeaderRow + 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            string? Cell(int? column) => column is { } index && index >= 0 && index < row.Count ? row[index].Trim() : null;
            var date = ParseDate(Cell(mapping.DateColumn));
            var amount = ParseAmount(Cell(mapping.AmountColumn));
            if (date is null || amount is null) continue;
            if (mapping.DirectionColumn is not null)
            {
                if (!string.Equals(Cell(mapping.DirectionColumn), mapping.IncomingValue?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                amount = Math.Abs(amount.Value);
            }
            if (amount <= 0) continue;
            lines.Add(new StatementLine(date.Value, amount.Value, Limit(Cell(mapping.DescriptionColumn) ?? string.Empty, 1_000),
                Empty(Limit(Cell(mapping.SenderColumn), 200)), Empty(Limit(Cell(mapping.ReferenceColumn), 100))));
        }
        return lines;
    }

    /// <summary>
    /// The IBAN a statement names in the lines above its header (Halkbank: "IBAN No:"); null when it names none. An
    /// upload for another account than the chosen one is refused with it.
    /// </summary>
    public static string? StatementIban(IReadOnlyList<IReadOnlyList<string>> rows, int headerRow)
    {
        foreach (var row in rows.Take(Math.Min(headerRow, rows.Count)))
            foreach (var cell in row)
                if (BillingRules.NormalizeIban(cell) is { } iban) return iban;
        return null;
    }

    /// <summary>The text of an uploaded CSV: UTF-8 when it is valid UTF-8, otherwise Turkish Windows (1254), as banks export it.</summary>
    public static string DecodeText(byte[] content)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(content).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(content);
        }
    }

    /// <summary>
    /// The identity of a statement line. Two transfers can be alike in everything, so the position among its twins in the
    /// file is part of it: the same file uploaded again adds nothing, and two real twins are both kept.
    /// </summary>
    public static string Fingerprint(Guid bankAccountId, StatementLine line, int occurrence)
    {
        var text = string.Join('|', bankAccountId.ToString("N"), line.BookedAt.ToString("O", CultureInfo.InvariantCulture),
            line.Amount.ToString("F2", CultureInfo.InvariantCulture), line.Description, line.Reference, occurrence.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Rows of a CSV text; the separator (; tab or ,) is the one the first lines use most. Quoted cells are understood.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ReadCsv(string text)
    {
        var sample = string.Join('\n', text.Split('\n').Take(10));
        var separator = new[] { ';', '\t', ',' }.OrderByDescending(candidate => sample.Count(character => character == candidate)).First();
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"') { cell.Append('"'); index++; }
                else if (character == '"') quoted = false;
                else cell.Append(character);
            }
            else if (character == '"' && cell.Length == 0) quoted = true;
            else if (character == separator) { row.Add(cell.ToString()); cell.Clear(); }
            else if (character is '\n' or '\r')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(cell.ToString());
                cell.Clear();
                if (row.Any(item => item.Length > 0)) rows.Add(row);
                row = [];
                if (rows.Count > MaxRows) break;
            }
            else cell.Append(character);
        }
        row.Add(cell.ToString());
        if (row.Any(item => item.Length > 0)) rows.Add(row);
        return rows;
    }

    private static string? Limit(string? value, int length) => value is { } text && text.Length > length ? text[..length] : value;
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // EDU, an optional dash or spaces, then six characters of the code alphabet, not glued to more letters or digits.
    [GeneratedRegex(@"(?<![A-Z0-9])EDU[\s\-_]{0,3}([23456789ABCDEFGHJKMNPQRSTUVWXYZ]{6})(?![A-Z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex PaymentCode();
}

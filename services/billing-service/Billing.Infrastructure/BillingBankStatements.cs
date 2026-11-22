using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>
/// Bank statements: the admin uploads the bank's export for one account; incoming lines are stored once, tied to the
/// waiting order whose payment code and amount they carry, and approved by themselves where every product of the order
/// is set to automatic approval. Only an exact match is ever approved without a person.
/// </summary>
public sealed class BillingBankStatements(BillingDbContext db, BillingOrders orders, TimeProvider clock) : IBillingBankStatements
{
    public const int MaxListed = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly System.Globalization.CultureInfo Turkish = new("tr-TR");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public StatementPreview Preview(string fileName, byte[] content)
    {
        var rows = ReadRows(fileName, content);
        return new StatementPreview(rows.Take(StatementRules.PreviewRows).ToArray(), rows.Count, StatementRules.SuggestMapping(rows));
    }

    public async Task<StatementImportResult> ImportAsync(Guid bankAccountId, string fileName, byte[] content, StatementMapping mapping, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var account = await db.BankAccounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == bankAccountId, cancellationToken)
            ?? throw new BusinessRuleException("Billing.BankAccountNotFound", "Banka hesabı bulunamadı.");
        var rows = ReadRows(fileName, content);
        if (mapping.HeaderRow < 0 || mapping.HeaderRow >= rows.Count || new[] { mapping.DateColumn, mapping.DescriptionColumn, mapping.AmountColumn }.Any(column => column is < 0 or > 200)
            || mapping.DirectionColumn is not null && string.IsNullOrWhiteSpace(mapping.IncomingValue))
            throw new BusinessRuleException("Billing.StatementInvalid", "Sütun eşleştirmesi geçersiz; tarih, açıklama ve tutar sütunlarını seçin.");
        // A statement that names its account must be the chosen account's: a wrong pick would tie payments to the wrong bank.
        if (StatementRules.StatementIban(rows, mapping.HeaderRow) is { } iban && iban != account.Iban)
            throw new BusinessRuleException("Billing.StatementWrongAccount", "Bu ekstre seçtiğiniz banka hesabına ait değil.");
        var lines = StatementRules.ReadLines(rows, mapping);
        if (lines.Count == 0)
            throw new BusinessRuleException("Billing.StatementEmpty", "Dosyada gelen ödeme satırı bulunamadı; sütun eşleştirmesini denetleyin.");

        var now = Now;
        var import = new BankStatementImport { Id = Guid.NewGuid(), BankAccountId = account.Id, FileName = SafeName(fileName), UploadedBy = actorId,
            UploadedAt = now, RowCount = lines.Count };
        db.BankStatementImports.Add(import);

        var occurrences = new Dictionary<string, int>();
        var candidates = lines.Select(line =>
        {
            var twin = StatementRules.Fingerprint(account.Id, line, 0);
            var occurrence = occurrences[twin] = occurrences.GetValueOrDefault(twin) + 1;
            return (line, fingerprint: StatementRules.Fingerprint(account.Id, line, occurrence));
        }).ToArray();
        var fingerprints = candidates.Select(item => item.fingerprint).ToArray();
        var known = (await db.BankTransactions.AsNoTracking().Where(item => fingerprints.Contains(item.Fingerprint)).Select(item => item.Fingerprint)
            .ToListAsync(cancellationToken)).ToHashSet();
        var added = candidates.Where(item => !known.Contains(item.fingerprint)).Select(item => new BankTransaction
        {
            Id = Guid.NewGuid(), BankAccountId = account.Id, ImportId = import.Id, BookedAt = item.line.BookedAt, Amount = item.line.Amount,
            Description = item.line.Description, Sender = item.line.Sender, Reference = item.line.Reference,
            PaymentCode = StatementRules.FindPaymentCode(item.line.Description), Fingerprint = item.fingerprint,
            Status = BankTransactionStatus.Unmatched, CreatedAt = now
        }).ToList();
        import.AddedCount = added.Count;
        import.DuplicateCount = candidates.Length - added.Count;
        db.BankTransactions.AddRange(added);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Billing.StatementBusy", "Aynı ekstre şu anda başka bir yüklemede işleniyor; biraz sonra yeniden deneyin.");
        }

        foreach (var line in added) await MatchAsync(line, now, cancellationToken);
        import.MatchedCount = added.Count(item => item.Status == BankTransactionStatus.Matched);
        import.AutoApprovedCount = added.Count(item => item.Status == BankTransactionStatus.Approved);
        await db.SaveChangesAsync(cancellationToken);

        var ids = added.Select(item => item.Id).ToArray();
        return new StatementImportResult((await ImportViewsAsync([import], cancellationToken))[0],
            await ViewsAsync(await db.BankTransactions.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.BookedAt).ToListAsync(cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Ties a new line to the waiting order its code names. Exact match only: a known code, the same amount, an order
    /// that still waits. Anything else stays for the admin, with the reason.
    /// </summary>
    private async Task MatchAsync(BankTransaction line, DateTime now, CancellationToken cancellationToken)
    {
        if (line.PaymentCode is null)
        {
            line.Note = "Açıklamada ödeme kodu yok.";
            return;
        }
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.PaymentCode == line.PaymentCode, cancellationToken);
        if (order is null)
        {
            line.Note = $"{line.PaymentCode} kodlu sipariş bulunamadı.";
            return;
        }
        line.OrderId = order.Id;
        if (order.Status != OrderStatus.Pending || order.ExpiresAt <= now)
        {
            line.Note = order.Status == OrderStatus.Paid ? "Bu sipariş zaten onaylanmış; ikinci ödeme olabilir." : "Sipariş artık ödeme beklemiyor.";
            return;
        }
        if (order.Amount != line.Amount)
        {
            line.Note = $"Tutar farklı: sipariş {order.Amount.ToString("N2", Turkish)} {order.Currency}.";
            return;
        }
        line.Status = BankTransactionStatus.Matched;
        if (!await IsAutomaticAsync(order, cancellationToken)) return;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            // Its own transaction, with the order and the line locked: an approval by hand at the same moment wins or loses cleanly.
            await orders.ApproveAsync(order.Id, null, null, line.Id, cancellationToken);
        }
        catch (BusinessRuleException exception)
        {
            // Somebody reviewed the order in between; the line stays for the admin with the reason.
            line.Status = BankTransactionStatus.Unmatched;
            line.Note = exception.Message;
        }
    }

    /// <summary>A bundle follows its strictest product: one product approved by hand makes the whole order wait for a person.</summary>
    private async Task<bool> IsAutomaticAsync(Order order, CancellationToken cancellationToken)
    {
        var codes = (JsonSerializer.Deserialize<OrderEntitlement[]>(order.EntitlementsJson, Json) ?? []).Select(item => item.ProductCode).Distinct().ToArray();
        var modes = await db.Products.AsNoTracking().Where(item => codes.Contains(item.Code)).Select(item => item.BankTransferApproval).ToListAsync(cancellationToken);
        return modes.Count == codes.Length && modes.Count > 0 && modes.All(mode => mode == BankTransferApproval.Automatic);
    }

    public async Task<IReadOnlyList<StatementImportView>> GetImportsAsync(CancellationToken cancellationToken = default) =>
        await ImportViewsAsync(await db.BankStatementImports.AsNoTracking().OrderByDescending(item => item.UploadedAt).Take(50).ToListAsync(cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<BankTransactionView>> GetTransactionsAsync(BankTransactionStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.BankTransactions.AsNoTracking();
        if (status is { } wanted) query = query.Where(item => item.Status == wanted);
        return await ViewsAsync(await query.OrderByDescending(item => item.BookedAt).Take(MaxListed).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<BankTransactionView?> ApproveWithOrderAsync(Guid transactionId, Guid orderId, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (await orders.ApproveAsync(orderId, actorId, null, transactionId, cancellationToken) is null) return null;
        var line = await db.BankTransactions.AsNoTracking().SingleAsync(item => item.Id == transactionId, cancellationToken);
        return (await ViewsAsync([line], cancellationToken))[0];
    }

    public async Task<BankTransactionView?> SetIgnoredAsync(Guid transactionId, bool ignored, string? note, Guid actorId, CancellationToken cancellationToken = default)
    {
        var text = note?.Trim();
        if (text is { Length: > 1_000 }) throw new BusinessRuleException("Billing.NoteRequired", "Not en fazla 1000 karakter olabilir.");
        var line = await db.BankTransactions.SingleOrDefaultAsync(item => item.Id == transactionId, cancellationToken);
        if (line is null) return null;
        if (line.Status == BankTransactionStatus.Approved)
            throw new BusinessRuleException("Billing.BankTransactionUsed", "Bu banka hareketi bir ödemede kullanıldı; yok sayılamaz.");
        if (ignored == (line.Status == BankTransactionStatus.Ignored)) return (await ViewsAsync([line], cancellationToken))[0];
        line.Status = ignored ? BankTransactionStatus.Ignored : BankTransactionStatus.Unmatched;
        if (!string.IsNullOrEmpty(text)) line.Note = text;
        line.ReviewedBy = actorId;
        line.ReviewedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return (await ViewsAsync([line], cancellationToken))[0];
    }

    // ---- Reading ------------------------------------------------------------------------------------------------------

    private static IReadOnlyList<IReadOnlyList<string>> ReadRows(string fileName, byte[] content)
    {
        if (content.Length is 0 or > StatementRules.MaxFileBytes)
            throw new BusinessRuleException("Billing.StatementInvalid", "Ekstre dosyası en fazla 5 MB olabilir.");
        IReadOnlyList<IReadOnlyList<string>> rows;
        // An .xlsx is a zip ("PK"); the old binary .xls starts with D0 CF and cannot be read.
        if (content.AsSpan().StartsWith("PK"u8)) rows = ReadXlsx(content);
        else if (content.AsSpan().StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }))
            throw new BusinessRuleException("Billing.StatementInvalid", "Eski Excel (.xls) dosyası okunamıyor; ekstreyi CSV ya da .xlsx olarak indirin.");
        else rows = StatementRules.ReadCsv(StatementRules.DecodeText(content));
        if (rows.Count == 0) throw new BusinessRuleException("Billing.StatementInvalid", "Dosya boş ya da okunamadı; CSV ya da .xlsx yükleyin.");
        if (rows.Count > StatementRules.MaxRows)
            throw new BusinessRuleException("Billing.StatementInvalid", "Dosyada 5000'den çok satır var; daha kısa bir tarih aralığı indirin.");
        return rows;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadXlsx(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbook = document.WorkbookPart ?? throw new InvalidDataException();
            var shared = workbook.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];
            var sheet = workbook.Workbook.Descendants<Sheet>().FirstOrDefault() ?? throw new InvalidDataException();
            var part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = ColumnIndex(cell.CellReference?.Value);
                    while (cells.Count < column) cells.Add(string.Empty);
                    var raw = cell.CellValue?.InnerText ?? cell.InnerText;
                    cells.Add(cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, out var index) && index >= 0 && index < shared.Length
                        ? shared[index] : raw);
                }
                if (cells.Any(item => item.Length > 0)) rows.Add(cells);
                if (rows.Count > StatementRules.MaxRows) break;
            }
            return rows;
        }
        catch (Exception exception) when (exception is not BusinessRuleException)
        {
            throw new BusinessRuleException("Billing.StatementInvalid", "Excel dosyası okunamadı; ekstreyi CSV olarak indirip yeniden deneyin.");
        }
    }

    /// <summary>"C7" → 2. Cells without a reference follow each other.</summary>
    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return 0;
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter)) index = index * 26 + (char.ToUpperInvariant(character) - 'A' + 1);
        return Math.Clamp(index - 1, 0, 200);
    }

    private static string SafeName(string? name)
    {
        var raw = name ?? string.Empty;
        var cut = raw.LastIndexOfAny(['/', '\\']);
        var file = new string((cut >= 0 ? raw[(cut + 1)..] : raw).Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (file.Length > 150) file = file[^150..];
        return file.Length > 0 ? file : "ekstre";
    }

    // ---- Views --------------------------------------------------------------------------------------------------------

    private async Task<IReadOnlyList<StatementImportView>> ImportViewsAsync(IReadOnlyList<BankStatementImport> imports, CancellationToken cancellationToken)
    {
        var names = await BankNamesAsync(imports.Select(item => item.BankAccountId), cancellationToken);
        return imports.Select(item => new StatementImportView(item.Id, item.BankAccountId, names.GetValueOrDefault(item.BankAccountId, "—"), item.FileName,
            item.UploadedAt, item.RowCount, item.AddedCount, item.DuplicateCount, item.MatchedCount, item.AutoApprovedCount)).ToArray();
    }

    private async Task<IReadOnlyList<BankTransactionView>> ViewsAsync(IReadOnlyList<BankTransaction> lines, CancellationToken cancellationToken)
    {
        var names = await BankNamesAsync(lines.Select(item => item.BankAccountId), cancellationToken);
        var orderIds = lines.Where(item => item.OrderId is not null).Select(item => item.OrderId!.Value).Distinct().ToArray();
        var summaries = await (from order in db.Orders.AsNoTracking()
                               join account in db.Accounts.AsNoTracking() on order.AccountId equals account.Id
                               where orderIds.Contains(order.Id)
                               select new { order.Id, account.DisplayName, order.PlanName, order.PaymentCode })
            .ToDictionaryAsync(item => item.Id, item => $"{item.DisplayName}, {item.PlanName}, {item.PaymentCode}", cancellationToken);
        return lines.Select(item => new BankTransactionView(item.Id, item.BankAccountId, names.GetValueOrDefault(item.BankAccountId, "—"), item.BookedAt,
            item.Amount, item.Description, item.Sender, item.PaymentCode, item.Status, item.OrderId,
            item.OrderId is { } orderId ? summaries.GetValueOrDefault(orderId) : null, item.Note)).ToArray();
    }

    private async Task<Dictionary<Guid, string>> BankNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToArray();
        return await db.BankAccounts.AsNoTracking().Where(item => wanted.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.BankName, cancellationToken);
    }
}

using System.Globalization;
using Coaching.Domain;
using Coaching.Infrastructure.Data;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingLicenseChange(DateTime? EndDate, int? SeatCapacity);

public sealed record CoachingLicenseRow(Guid Id, string Audience, string PlanName, string? OwnerName, string? OwnerEmail,
    Guid? UserId, Guid? InstitutionId, string Status, DateTime StartDate, DateTime EndDate, int DaysLeft, int? SeatCapacity, int AssignedSeats);

public sealed record CoachingChurnRow(string Month, string Audience, int Ended, int Renewed, int NotRenewed, int Pending, decimal? ChurnRate);

/// <summary>
/// Roadmap D6: tools for the platform administrator of coaching licenses.
/// <list type="bullet">
/// <item>A license's end date (its last paid period moves with it) and an institution's seat capacity
/// (at least the plan's seats and the seats in use; the difference is kept as extra seats).</item>
/// <item>Licenses ending in the next 30 days and those that ended in the last 30 days.</item>
/// <item>Payments as an Excel sheet; text cells starting with = + - @ are prefixed with ' so Excel never runs them.</item>
/// <item>Churn: of the paid periods that ended in a month (Türkiye time), how many were followed by another period of
/// the same owner and audience within 30 days. Trials are left out; a period whose 30 days are not over yet is pending.</item>
/// </list>
/// </summary>
public sealed class CoachingAdminToolsService(CoachingDbContext db, TimeProvider clock)
{
    public const int WindowDays = 30;
    public const int RenewalDays = 30;
    public const int MaxSeats = 100_000;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<CoachingLicenseRow> UpdateLicenseAsync(Guid id, CoachingLicenseChange change, Guid actor, CancellationToken ct)
    {
        var license = await db.CoachingSubscriptions.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Subscription.NotFound", "Lisans bulunamadı.");
        var plan = await db.CoachingSubscriptionPlans.SingleAsync(x => x.Id == license.PlanId, ct);
        if (change.EndDate is { } requested)
        {
            var end = DateTime.SpecifyKind(CoachingTime.ToUtc(requested), DateTimeKind.Utc);
            var last = await db.CoachingSubscriptionPeriods.Where(x => x.SubscriptionId == id && x.Status == "Active")
                .OrderByDescending(x => x.EndDate).FirstOrDefaultAsync(ct);
            if (end <= license.StartDate || last is not null && end <= last.StartDate)
                throw new BusinessRuleException("Subscription.EndBeforeStart", "Bitiş tarihi lisansın ve son ödenen dönemin başlangıcından sonra olmalı.");
            license.EndDate = end;
            if (last is not null) { last.EndDate = end; last.DurationDays = Math.Max(1, (int)Math.Ceiling((end - last.StartDate).TotalDays)); }
            if (license.Status == "Expired" && end > Now) license.Status = "Active";
            if (license.Status == "Active" && end <= Now) license.Status = "Expired";
        }
        if (change.SeatCapacity is { } capacity)
        {
            if (plan.Audience != "Institution" || license.InstitutionId is null || plan.IncludedStudentSeats is null)
                throw new BusinessRuleException("Subscription.SeatsNotApplicable", "Koltuk sayısı yalnız kurum lisanslarında değiştirilir.");
            if (capacity < plan.IncludedStudentSeats.Value)
                throw new BusinessRuleException("Subscription.SeatsBelowPlan", $"Koltuk sayısı plandaki {plan.IncludedStudentSeats} koltuktan az olamaz.");
            var assigned = await db.CoachingSubscriptionSeats.CountAsync(x => x.SubscriptionId == id && !x.IsSuspended, ct);
            if (capacity < assigned || capacity > MaxSeats)
                throw new BusinessRuleException("Subscription.SeatsBelowAssigned", $"Koltuk sayısı kullanımdaki {assigned} koltuktan az olamaz.");
            license.ExtraSeats = capacity - plan.IncludedStudentSeats.Value;
        }
        license.UpdatedBy = actor; license.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return (await RowsAsync(db.CoachingSubscriptions.Where(x => x.Id == id), ct)).Single();
    }

    /// <summary><c>ending</c>: active and ending in the next 30 days; <c>ended</c>: ended in the last 30 days.</summary>
    public async Task<IReadOnlyList<CoachingLicenseRow>> WindowAsync(string window, CancellationToken ct)
    {
        var now = Now;
        var query = window switch
        {
            "ending" => db.CoachingSubscriptions.Where(x => x.Status == "Active" && x.EndDate > now && x.EndDate <= now.AddDays(WindowDays)),
            "ended" => db.CoachingSubscriptions.Where(x => x.Status != "Cancelled" && x.EndDate <= now && x.EndDate > now.AddDays(-WindowDays)),
            _ => throw new ArgumentException("Pencere 'ending' ya da 'ended' olmalı.")
        };
        var rows = await RowsAsync(query, ct);
        return window == "ending" ? rows.OrderBy(x => x.EndDate).ToArray() : rows.OrderByDescending(x => x.EndDate).ToArray();
    }

    public async Task<byte[]> ExportPaymentsAsync(DateOnly? from, DateOnly? to, string? status, CancellationToken ct)
    {
        var query = db.CoachingPaymentRecords.AsNoTracking().AsQueryable();
        if (from is { } first) { var start = TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), CoachingTime.Turkey); query = query.Where(x => x.CreatedAt >= start); }
        if (to is { } last) { var end = TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), CoachingTime.Turkey); query = query.Where(x => x.CreatedAt < end); }
        if (!string.IsNullOrWhiteSpace(status)) { var wanted = status.Trim(); query = query.Where(x => x.Status == wanted); }
        var payments = await query.OrderBy(x => x.CreatedAt).Take(50_000).ToListAsync(ct);

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
            var sheetPart = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            data.Append(Row(["Tarih", "Ad", "E-posta", "Plan", "Tutar", "Para birimi", "Durum", "Yöntem", "Referans", "Kurum"]));
            foreach (var p in payments)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc), CoachingTime.Turkey);
                data.Append(Row([local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture), p.UserName, p.UserEmail, p.PlanName, p.Amount,
                    p.Currency, p.Status, p.Provider, p.Reference ?? "", p.InstitutionId?.ToString() ?? ""]));
            }
            sheetPart.Worksheet = new Worksheet(data);
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheetPart), SheetId = 1, Name = "Ödemeler" }));
            workbook.Workbook.Save();
        }
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<CoachingChurnRow>> ChurnAsync(int months, CancellationToken ct)
    {
        if (months is < 1 or > 24) throw new ArgumentException("1–24 ay seçin.");
        var now = Now;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, CoachingTime.Turkey);
        var firstMonth = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(-(months - 1));
        var since = TimeZoneInfo.ConvertTimeToUtc(firstMonth, CoachingTime.Turkey);
        var periods = await (from period in db.CoachingSubscriptionPeriods.AsNoTracking()
                             join license in db.CoachingSubscriptions.AsNoTracking() on period.SubscriptionId equals license.Id
                             join plan in db.CoachingSubscriptionPlans.AsNoTracking() on license.PlanId equals plan.Id
                             where period.Status == "Active" && period.Source != "Trial"
                             select new { period.Id, period.StartDate, period.EndDate, plan.Audience, license.UserId, license.InstitutionId })
            .ToListAsync(ct);
        var byOwner = periods.GroupBy(x => (x.Audience, Owner: x.InstitutionId ?? x.UserId)).ToDictionary(x => x.Key, x => x.ToArray());
        var counted = periods.Where(x => x.EndDate >= since && x.EndDate <= now).Select(p =>
        {
            var renewed = byOwner[(p.Audience, p.InstitutionId ?? p.UserId)].Any(q => q.Id != p.Id && q.EndDate > p.EndDate
                && q.StartDate <= p.EndDate.AddDays(RenewalDays) && q.StartDate >= p.StartDate);
            var outcome = renewed ? "Renewed" : p.EndDate.AddDays(RenewalDays) > now ? "Pending" : "NotRenewed";
            var month = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.EndDate, DateTimeKind.Utc), CoachingTime.Turkey).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return (Month: month, p.Audience, Outcome: outcome);
        }).ToArray();
        var result = new List<CoachingChurnRow>();
        for (var month = firstMonth; month <= localNow; month = month.AddMonths(1))
        {
            var key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            foreach (var audience in new[] { "Individual", "Teacher", "Institution" })
            {
                var rows = counted.Where(x => x.Month == key && x.Audience == audience).ToArray();
                int Count(string outcome) => rows.Count(x => x.Outcome == outcome);
                var decided = Count("Renewed") + Count("NotRenewed");
                result.Add(new(key, audience, rows.Length, Count("Renewed"), Count("NotRenewed"), Count("Pending"),
                    decided == 0 ? null : Math.Round(100m * Count("NotRenewed") / decided, 1)));
            }
        }
        return result;
    }

    private async Task<IReadOnlyList<CoachingLicenseRow>> RowsAsync(IQueryable<Domain.Entities.CoachingSubscription> licenses, CancellationToken ct)
    {
        var now = Now;
        var rows = await (from license in licenses.AsNoTracking()
                          join plan in db.CoachingSubscriptionPlans.AsNoTracking() on license.PlanId equals plan.Id
                          select new { license, plan.Audience, plan.Name, plan.IncludedStudentSeats }).ToListAsync(ct);
        var ids = rows.Select(x => x.license.Id).ToArray();
        var seats = await db.CoachingSubscriptionSeats.AsNoTracking().Where(x => Enumerable.Contains(ids, x.SubscriptionId) && !x.IsSuspended)
            .GroupBy(x => x.SubscriptionId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return rows.Select(x => new CoachingLicenseRow(x.license.Id, x.Audience, x.Name, x.license.UserName, x.license.UserEmail,
            x.license.UserId, x.license.InstitutionId, x.license.Status, x.license.StartDate, x.license.EndDate,
            (int)Math.Floor((x.license.EndDate - now).TotalDays), x.IncludedStudentSeats is { } included ? included + x.license.ExtraSeats : null,
            seats.GetValueOrDefault(x.license.Id))).ToArray();
    }

    private static Row Row(IEnumerable<object> values)
    {
        var row = new Row();
        foreach (var value in values)
            row.Append(value is decimal number
                ? new Cell { DataType = CellValues.Number, CellValue = new CellValue(number.ToString(CultureInfo.InvariantCulture)) }
                : new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(Safe(value?.ToString() ?? ""))) });
        return row;
    }

    /// <summary>Text that Excel would read as a formula is kept as text.</summary>
    private static string Safe(string text) => text.TrimStart().Length > 0 && "=+-@\t\r".Contains(text.TrimStart()[0]) ? "'" + text : text;
}

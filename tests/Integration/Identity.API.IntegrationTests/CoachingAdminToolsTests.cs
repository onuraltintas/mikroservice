using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using DocumentFormat.OpenXml.Packaging;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap D6: the platform administrator edits a license's end date and seats, sees ending and ended licenses,
// exports payments as Excel and reads a churn report (licenses not renewed within 30 days, by audience).
public sealed class CoachingAdminToolsTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Admin = Guid.NewGuid();

    private static CoachingDbContext Db() => new(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (CoachingSubscriptionPlan Plan, CoachingSubscription License, CoachingSubscriptionPeriod Period) Institution(CoachingDbContext db, DateTime start, DateTime end, int seats = 10)
    {
        var plan = new CoachingSubscriptionPlan { Name = "Kurum yıllık", Slug = "kurum-" + Guid.NewGuid().ToString("N")[..6], Audience = "Institution", DurationDays = 365, IncludedStudentSeats = seats };
        var license = new CoachingSubscription { PlanId = plan.Id, InstitutionId = Guid.NewGuid(), UserName = "Merkez", StartDate = start, EndDate = end };
        var period = new CoachingSubscriptionPeriod { SubscriptionId = license.Id, StartDate = start, EndDate = end, DurationDays = (end - start).Days, Source = "Manual" };
        db.AddRange(plan, license, period); return (plan, license, period);
    }

    [Fact]
    public async Task LicenseEndDateAndSeatsChangeTogetherWithItsLastPeriod()
    {
        await using var db = Db(); var (_, license, period) = Institution(db, Now.AddDays(-30), Now.AddDays(335));
        for (var i = 0; i < 4; i++) db.Add(new CoachingSubscriptionSeat { SubscriptionId = license.Id, StudentId = Guid.NewGuid(), AssignedBy = Admin });
        await db.SaveChangesAsync();
        var tools = new CoachingAdminToolsService(db, new Clock(Now));

        var changed = await tools.UpdateLicenseAsync(license.Id, new(Now.AddDays(400), 15), Admin, default);
        Assert.Equal((Now.AddDays(400), 15, 5), (changed.EndDate, changed.SeatCapacity, (await db.CoachingSubscriptions.SingleAsync()).ExtraSeats));
        Assert.Equal(Now.AddDays(400), (await db.CoachingSubscriptionPeriods.SingleAsync()).EndDate);

        var below = await Assert.ThrowsAsync<BusinessRuleException>(() => tools.UpdateLicenseAsync(license.Id, new(null, 9), Admin, default));
        Assert.Equal("Subscription.SeatsBelowPlan", below.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => tools.UpdateLicenseAsync(license.Id, new(period.StartDate.AddDays(-1), null), Admin, default));
        var individualPlan = new CoachingSubscriptionPlan { Name = "Bireysel", Slug = "bireysel", Audience = "Individual", DurationDays = 30 };
        var individual = new CoachingSubscription { PlanId = individualPlan.Id, UserId = Guid.NewGuid(), StartDate = Now, EndDate = Now.AddDays(30) };
        db.AddRange(individualPlan, individual); await db.SaveChangesAsync();
        Assert.Equal("Subscription.SeatsNotApplicable",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => tools.UpdateLicenseAsync(individual.Id, new(null, 3), Admin, default))).Code);
    }

    [Fact]
    public async Task EndingAndEndedLicensesAreListedByDaysLeft()
    {
        await using var db = Db();
        var (_, soon, _) = Institution(db, Now.AddDays(-360), Now.AddDays(5));
        var (_, later, _) = Institution(db, Now.AddDays(-300), Now.AddDays(65));
        var (_, ended, _) = Institution(db, Now.AddDays(-380), Now.AddDays(-10)); ended.Status = "Expired";
        var (_, longAgo, _) = Institution(db, Now.AddDays(-500), Now.AddDays(-100)); longAgo.Status = "Expired";
        await db.SaveChangesAsync();
        var tools = new CoachingAdminToolsService(db, new Clock(Now));
        Assert.Equal(new[] { soon.Id }, (await tools.WindowAsync("ending", default)).Select(x => x.Id));
        var endedRows = await tools.WindowAsync("ended", default);
        Assert.Equal(new[] { ended.Id }, endedRows.Select(x => x.Id)); Assert.Equal(-10, endedRows[0].DaysLeft);
        await Assert.ThrowsAsync<ArgumentException>(() => tools.WindowAsync("other", default));
    }

    [Fact]
    public async Task PaymentsAreExportedAsAnExcelSheetWithoutFormulaInjection()
    {
        await using var db = Db();
        db.AddRange(
            new CoachingPaymentRecord { UserName = "=HYPERLINK(\"x\")", UserEmail = "a@example.test", PlanName = "Bireysel", Amount = 1200.5m, CreatedAt = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc), Reference = "EFT-1" },
            new CoachingPaymentRecord { UserName = "Eski", UserEmail = "b@example.test", PlanName = "Bireysel", Amount = 10, CreatedAt = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var bytes = await new CoachingAdminToolsService(db, new Clock(Now)).ExportPaymentsAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, default);
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var part = document.WorkbookPart!.WorksheetParts.Single();
        var cells = part.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>().Select(c => c.InnerText).ToArray();
        Assert.Contains("Tutar", cells); Assert.Contains("'=HYPERLINK(\"x\")", cells); Assert.Contains("1200.5", cells);
        Assert.DoesNotContain("Eski", cells);
    }

    [Fact]
    public async Task ChurnCountsLicensesNotRenewedWithinThirtyDaysByAudience()
    {
        await using var db = Db();
        var individual = new CoachingSubscriptionPlan { Name = "Bireysel", Slug = "b", Audience = "Individual", DurationDays = 30 };
        db.Add(individual);
        CoachingSubscriptionPeriod Period(Guid user, DateTime start, DateTime end, string source = "BankTransfer")
        {
            var license = new CoachingSubscription { PlanId = individual.Id, UserId = user, StartDate = start, EndDate = end };
            var period = new CoachingSubscriptionPeriod { SubscriptionId = license.Id, StartDate = start, EndDate = end, DurationDays = 30, Source = source };
            db.AddRange(license, period); return period;
        }
        // September: one renewed (next period starts 10 days later), one lost, one trial (not counted).
        var renewed = Guid.NewGuid(); Period(renewed, new(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc), new(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        Period(renewed, new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));
        Period(Guid.NewGuid(), new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        Period(Guid.NewGuid(), new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc), "Trial");
        // The institution license of September was renewed by a new license 40 days later: lost.
        var (_, _, _) = Institution(db, new(2025, 9, 25, 0, 0, 0, DateTimeKind.Utc), new(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();
        var report = await new CoachingAdminToolsService(db, new Clock(Now)).ChurnAsync(3, default);
        var september = report.Where(x => x.Month == "2026-09").ToDictionary(x => x.Audience);
        Assert.Equal((2, 1, 1, 0, 50m), (september["Individual"].Ended, september["Individual"].Renewed, september["Individual"].NotRenewed, september["Individual"].Pending, september["Individual"].ChurnRate));
        Assert.Equal((1, 0, 1), (september["Institution"].Ended, september["Institution"].Renewed, september["Institution"].NotRenewed));
        // October's renewed period ended on 20.10; its 30-day window is still open on 15.11.
        var october = report.Single(x => x.Month == "2026-10" && x.Audience == "Individual");
        Assert.Equal((1, 1, null), (october.Ended, october.Pending, october.ChurnRate));
    }
}

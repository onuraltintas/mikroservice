using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed class CourseEmailOptions
{
    public const string SectionName = "CourseManagement:Email";
    public const string DevelopmentKey = "course-management-development-unsubscribe-key-only";

    /// <summary>Address that serves /api (the institution portal); unsubscribe links start here.</summary>
    public string PublicBaseUrl { get; init; } = "https://kurum.eduivme.com";
    /// <summary>Signs unsubscribe links. Production requires its own value of at least 32 characters.</summary>
    public string UnsubscribeKey { get; init; } = DevelopmentKey;
    /// <summary>How long a mark may still be corrected before anyone is told (absence, homework, lesson change, payment).</summary>
    public int DelayMinutes { get; init; } = 10;
    public int PollingSeconds { get; init; } = 60;
    /// <summary>How often sent e-mails' delivery is read back from the notification service.</summary>
    public int DeliveryCheckSeconds { get; init; } = 300;

    public static string DeriveKey(string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("course-management-email-unsubscribe-v1")));
    }
}

/// <summary>Signed, link-only unsubscribe: the address holder needs no account.</summary>
public static class CourseEmailLinks
{
    public static string Token(CourseEmailOptions options, Guid workspaceId, string email)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.UnsubscribeKey));
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{workspaceId:N}:{email.Trim().ToLowerInvariant()}")));
    }

    public static bool IsValid(CourseEmailOptions options, Guid workspaceId, string email, string? token) =>
        token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Token(options, workspaceId, email)), Encoding.ASCII.GetBytes(token));

    public static string UnsubscribeUrl(CourseEmailOptions options, Guid workspaceId, string email) =>
        $"{options.PublicBaseUrl.TrimEnd('/')}/api/course-management/public/email/unsubscribe?w={workspaceId:N}"
        + $"&e={Base64Url(Encoding.UTF8.GetBytes(email))}&t={Token(options, workspaceId, email)}";

    public static string? DecodeEmail(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 600) return null;
        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException) { return null; }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Stable message id per e-mail and address, so a retried send is not delivered twice.</summary>
    public static Guid MessageId(Guid emailId, string address)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{emailId:N}:{address}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }
}

/// <summary>
/// Finds what happened (marks, changes, payments, due dates), queues one e-mail per student and occasion, and sends the
/// queued ones when their correction window is over. Everything is re-checked at sending time: a corrected mark, a
/// reversed payment or a paid installment sends nothing.
/// </summary>
public sealed class CourseEmailWorker(IServiceScopeFactory scopeFactory, CourseEmailOptions options, TimeProvider clock,
    ILogger<CourseEmailWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan Lookback = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var polling = TimeSpan.FromSeconds(Math.Clamp(options.PollingSeconds, 1, 600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Course management e-mail run failed."); }

            try { await Task.Delay(polling, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using (var scope = scopeFactory.CreateAsyncScope())
            await CourseEmailCollector.CollectAsync(scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>(),
                options, clock.GetUtcNow(), ct);
        await using (var scope = scopeFactory.CreateAsyncScope())
            await CourseEmailSender.SendDueAsync(scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(), options, clock.GetUtcNow(), ct,
                scope.ServiceProvider.GetService<WorkspaceBillingPlans>());
        // Each of these is on its own: a missing identity or notification answer must not stop the institution e-mails.
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await SupportAccessNotices.SendDueAsync(scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(), scope.ServiceProvider.GetRequiredService<IdentityAccountLookup>(),
                options, clock.GetUtcNow(), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        { logger.LogWarning(exception, "Support access notices could not be sent this time."); }
        if (clock.GetUtcNow() - lastDeliveryCheck >= TimeSpan.FromSeconds(Math.Clamp(options.DeliveryCheckSeconds, 1, 3600)))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await CourseEmailDeliveryCheck.RunAsync(scope.ServiceProvider.GetRequiredService<CourseManagementDbContext>(),
                    scope.ServiceProvider.GetRequiredService<NotificationDeliveryClient>(), clock.GetUtcNow(), ct,
                    TimeSpan.FromSeconds(Math.Clamp(options.DeliveryCheckSeconds, 1, 60)));
                lastDeliveryCheck = clock.GetUtcNow();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
            { logger.LogWarning(exception, "E-mail delivery results could not be read this time."); }
        }
    }

    private DateTimeOffset lastDeliveryCheck = DateTimeOffset.MinValue;
}

public static class CourseEmailCollector
{
    public static DateOnly TurkeyDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, CourseManagementTime.Turkey).DateTime);
    public static DateTimeOffset TurkeyNow(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, CourseManagementTime.Turkey);

    public static async Task CollectAsync(CourseManagementDbContext db, CourseEmailOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await db.EmailSettings.AsNoTracking().Where(item => item.InformedConsentAt != null).ToListAsync(ct);
        foreach (var workspace in settings)
        {
            if (!Enum.GetValues<CourseEmailKind>().Any(workspace.IsEnabled)) continue;
            await CollectWorkspaceAsync(db, workspace, options, now, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private static async Task CollectWorkspaceAsync(CourseManagementDbContext db, WorkspaceEmailSettings settings,
        CourseEmailOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var workspaceId = settings.WorkspaceId;
        var since = now - CourseEmailWorker.Lookback;
        var settled = now - TimeSpan.FromMinutes(Math.Max(0, options.DelayMinutes));
        var local = TurkeyNow(now);
        var today = DateOnly.FromDateTime(local.DateTime);

        if (settings.IsEnabled(CourseEmailKind.Absence))
        {
            var absences = await (from attendance in db.AttendanceRecords.AsNoTracking()
                join lesson in db.LessonSessions.AsNoTracking() on attendance.LessonSessionId equals lesson.Id
                where attendance.WorkspaceId == workspaceId && attendance.Status == LessonAttendanceStatus.Absent
                    && attendance.MarkedAt >= since && attendance.MarkedAt <= settled
                    // A support correction tells no one.
                    && !db.SupportAccessGrants.Any(grant => grant.WorkspaceId == workspaceId && grant.AdminUserId == attendance.MarkedByUserId)
                select new { attendance.StudentId, attendance.LessonSessionId, lesson.StartsAt }).ToListAsync(ct);
            foreach (var group in absences.GroupBy(item => (item.StudentId, Day: TurkeyDate(item.StartsAt))))
                await GatherAsync(db, workspaceId, CourseEmailKind.Absence, group.Key.StudentId, $"absence:{group.Key.StudentId:N}:{group.Key.Day:yyyyMMdd}",
                    group.Select(item => item.LessonSessionId), now, ct);
        }

        if (settings.IsEnabled(CourseEmailKind.HomeworkNotDone))
        {
            var marks = await db.AssignmentRecipients.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && (item.Completion == CourseAssignmentCompletion.NotDone || item.Completion == CourseAssignmentCompletion.Partial)
                    && item.CompletionMarkedAt >= since && item.CompletionMarkedAt <= settled)
                .Select(item => new { item.StudentId, item.AssignmentId, item.CompletionMarkedAt }).ToListAsync(ct);
            foreach (var group in marks.GroupBy(item => (item.StudentId, Day: TurkeyDate(item.CompletionMarkedAt!.Value))))
                await GatherAsync(db, workspaceId, CourseEmailKind.HomeworkNotDone, group.Key.StudentId,
                    $"homework:{group.Key.StudentId:N}:{group.Key.Day:yyyyMMdd}", group.Select(item => item.AssignmentId), now, ct);
        }

        if (settings.IsEnabled(CourseEmailKind.LessonChanged))
        {
            string[] lessonActions = ["LessonSession.Cancelled", "LessonSession.Rescheduled", "LessonSession.SeriesSlotCancelled"];
            string[] seriesActions = ["LessonSeries.ScheduleUpdated", "LessonSeries.FutureCancelled"];
            var changes = await db.AuditEntries.AsNoTracking().Where(entry => entry.WorkspaceId == workspaceId
                    && entry.OccurredAt >= since && entry.OccurredAt <= settled
                    && (lessonActions.Contains(entry.Action) || seriesActions.Contains(entry.Action)))
                .Select(entry => new { entry.TargetId, entry.Action, entry.OccurredAt }).ToListAsync(ct);
            var touched = new List<(Guid StudentId, Guid Reference, DateTimeOffset At)>();
            var lessonIds = changes.Where(change => lessonActions.Contains(change.Action)).Select(change => change.TargetId).Distinct().ToArray();
            if (lessonIds.Length > 0)
            {
                var participants = await (from participant in db.LessonParticipants.AsNoTracking()
                    join lesson in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals lesson.Id
                    where participant.WorkspaceId == workspaceId && Enumerable.Contains(lessonIds, participant.LessonSessionId)
                        && participant.IsActive && lesson.StartsAt > now
                    select new { participant.StudentId, participant.LessonSessionId }).ToListAsync(ct);
                touched.AddRange(participants.Select(item => (item.StudentId, item.LessonSessionId,
                    changes.First(change => change.TargetId == item.LessonSessionId).OccurredAt)));
            }
            var seriesIds = changes.Where(change => seriesActions.Contains(change.Action)).Select(change => change.TargetId).Distinct().ToArray();
            if (seriesIds.Length > 0)
            {
                var series = await db.LessonSeries.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && Enumerable.Contains(seriesIds, item.Id)).Select(item => new { item.Id, item.GroupId, item.StudentId }).ToListAsync(ct);
                var groupIds = series.Where(item => item.GroupId != null).Select(item => item.GroupId!.Value).ToArray();
                var enrolled = await db.Enrollments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && Enumerable.Contains(groupIds, item.GroupId) && item.Status == EnrollmentStatus.Active)
                    .Select(item => new { item.GroupId, item.StudentId }).ToListAsync(ct);
                foreach (var item in series)
                {
                    var at = changes.First(change => change.TargetId == item.Id).OccurredAt;
                    if (item.StudentId is Guid single) touched.Add((single, item.Id, at));
                    touched.AddRange(enrolled.Where(row => row.GroupId == item.GroupId).Select(row => (row.StudentId, item.Id, at)));
                }
            }
            foreach (var group in touched.GroupBy(item => (item.StudentId, Day: TurkeyDate(item.At))))
                await GatherAsync(db, workspaceId, CourseEmailKind.LessonChanged, group.Key.StudentId,
                    $"lesson:{group.Key.StudentId:N}:{group.Key.Day:yyyyMMdd}", group.Select(item => item.Reference), now, ct);
        }

        if (settings.IsEnabled(CourseEmailKind.PaymentReceived))
        {
            var payments = await (from entry in db.AuditEntries.AsNoTracking()
                join payment in db.Payments.AsNoTracking() on entry.TargetId equals payment.Id
                where entry.WorkspaceId == workspaceId && entry.Action == "Finance.PaymentRecorded"
                    && entry.OccurredAt >= since && entry.OccurredAt <= settled && payment.ReversalOfPaymentId == null
                select new { payment.Id, payment.StudentId }).ToListAsync(ct);
            foreach (var payment in payments)
                await CreateOnceAsync(db, workspaceId, CourseEmailKind.PaymentReceived, payment.StudentId, $"payment:{payment.Id:N}",
                    [payment.Id], now, ct);
        }

        var scheduledHour = local.Hour >= settings.SendHour && local.Hour <= 21;
        if (scheduledHour && (settings.IsEnabled(CourseEmailKind.InstallmentReminder) || settings.IsEnabled(CourseEmailKind.InstallmentOverdue)))
        {
            var dates = new Dictionary<DateOnly, (CourseEmailKind Kind, string Occasion)>();
            if (settings.IsEnabled(CourseEmailKind.InstallmentReminder))
            {
                dates[today.AddDays(settings.InstallmentDaysBefore)] = (CourseEmailKind.InstallmentReminder, "before");
                if (settings.InstallmentOnDueDay) dates[today] = (CourseEmailKind.InstallmentReminder, "due");
            }
            if (settings.IsEnabled(CourseEmailKind.InstallmentOverdue))
            {
                dates[today.AddDays(-settings.OverdueDaysAfter)] = (CourseEmailKind.InstallmentOverdue, "overdue1");
                dates[today.AddDays(-2 * settings.OverdueDaysAfter)] = (CourseEmailKind.InstallmentOverdue, "overdue2");
            }
            var dueDates = dates.Keys.ToArray();
            var installments = await CourseEmailContent.OpenInstallmentsAsync(db, workspaceId,
                query => query.Where(item => Enumerable.Contains(dueDates, item.DueOn)), ct);
            foreach (var installment in installments)
            {
                var (kind, occasion) = dates[installment.DueOn];
                await CreateOnceAsync(db, workspaceId, kind, installment.StudentId, $"installment:{installment.Id:N}:{occasion}",
                    [installment.Id], now, ct);
            }
        }

        if (settings.IsEnabled(CourseEmailKind.WeeklySchedule) && local.DayOfWeek == DayOfWeek.Sunday && local.Hour is >= 18 and <= 21)
        {
            var (weekStart, weekEnd) = CourseEmailContent.NextWeek(now);
            var students = await (from participant in db.LessonParticipants.AsNoTracking()
                join lesson in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals lesson.Id
                where participant.WorkspaceId == workspaceId && participant.IsActive && lesson.Status == LessonSessionStatus.Scheduled
                    && lesson.StartsAt >= weekStart && lesson.StartsAt < weekEnd
                select participant.StudentId).Distinct().ToListAsync(ct);
            var week = DateOnly.FromDateTime(TurkeyNow(weekStart).DateTime);
            foreach (var studentId in students)
                await CreateOnceAsync(db, workspaceId, CourseEmailKind.WeeklySchedule, studentId, $"weekly:{studentId:N}:{week:yyyyMMdd}",
                    null, now, ct);
        }
    }

    /// <summary>
    /// Adds new occasions (lessons, assignments) to the student's waiting e-mail of the day, or starts a new one when the
    /// last one already went out. An occasion is told once.
    /// </summary>
    internal static async Task GatherAsync(CourseManagementDbContext db, Guid workspaceId, CourseEmailKind kind, Guid studentId,
        string dayKey, IEnumerable<Guid> references, DateTimeOffset now, CancellationToken ct)
    {
        var prefix = dayKey + ":";
        var existing = await db.Emails.Where(item => item.WorkspaceId == workspaceId && item.DedupKey.StartsWith(prefix))
            .ToListAsync(ct);
        existing.AddRange(db.Emails.Local.Where(item => item.WorkspaceId == workspaceId && item.DedupKey.StartsWith(prefix)
            && !existing.Contains(item)));
        var known = existing.SelectMany(item => item.References).ToHashSet();
        var fresh = references.Distinct().Where(id => !known.Contains(id)).ToArray();
        if (fresh.Length == 0) return;
        var waiting = existing.FirstOrDefault(item => item.Status == CourseEmailStatus.Pending);
        if (waiting is not null) waiting.AddReferences(fresh);
        else db.Emails.Add(CourseEmail.Create(workspaceId, kind, studentId, $"{dayKey}:{existing.Count + 1}", now, now, fresh));
    }

    internal static async Task CreateOnceAsync(CourseManagementDbContext db, Guid workspaceId, CourseEmailKind kind, Guid studentId,
        string key, IEnumerable<Guid>? references, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Emails.Local.Any(item => item.WorkspaceId == workspaceId && item.DedupKey == key)
            || await db.Emails.AnyAsync(item => item.WorkspaceId == workspaceId && item.DedupKey == key, ct)) return;
        db.Emails.Add(CourseEmail.Create(workspaceId, kind, studentId, key, now, now, references));
    }
}

public static class CourseEmailSender
{
    public static async Task SendDueAsync(CourseManagementDbContext db, IPublishEndpoint publish, CourseEmailOptions options,
        DateTimeOffset now, CancellationToken ct, WorkspaceBillingPlans? billing = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var due = await db.Emails.FromSqlInterpolated($"""
            SELECT * FROM "CourseEmails"
            WHERE "Status" = {(int)CourseEmailStatus.Pending} AND "DueAt" <= {now}
            ORDER BY "DueAt"
            LIMIT 100
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var paused = new Dictionary<Guid, bool>();
        foreach (var email in due)
        {
            if (!paused.TryGetValue(email.WorkspaceId, out var noPlan))
                paused[email.WorkspaceId] = noPlan = !await HasPlanAsync(db, billing, email.WorkspaceId, now, ct);
            if (noPlan) { email.Skip("Kurumun aboneliği bitti", now); continue; }
            await SendAsync(db, publish, options, email, now, ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// A workspace whose plan has ended is read-only, and it sends nothing either: from the billing service where it runs,
    /// otherwise from the workspace subscription (none recorded means not limited, as for writes).
    /// </summary>
    public static async Task<bool> HasPlanAsync(CourseManagementDbContext db, WorkspaceBillingPlans? billing, Guid workspaceId,
        DateTimeOffset now, CancellationToken ct)
    {
        if (billing is { Enabled: true }) return (await billing.GetForWorkspaceAsync(db, workspaceId, ct)).CanWriteAt(now);
        var subscription = await db.WorkspaceSubscriptions.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
        return subscription is null || subscription.CanWriteAt(now);
    }

    private static async Task SendAsync(CourseManagementDbContext db, IPublishEndpoint publish, CourseEmailOptions options,
        CourseEmail email, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == email.WorkspaceId, ct);
        if (settings is null || !settings.IsEnabled(email.Kind)) { email.Skip("Bildirim kapalı", now); return; }
        var workspace = await db.Workspaces.AsNoTracking().SingleAsync(item => item.Id == email.WorkspaceId, ct);
        if (workspace.IsErasureReadOnly) { email.Skip("Kurum salt okunur", now); return; }
        var student = await db.Students.AsNoTracking().SingleAsync(item => item.Id == email.StudentId, ct);
        if (!student.IsActive) { email.Skip("Öğrenci arşivde", now); return; }
        if (student.EmailsMuted) { email.Skip("Öğrenci için kapalı", now); return; }

        var content = await CourseEmailContent.ComposeAsync(db, email, student, now, ct);
        if (content is null) { email.Skip("Artık geçerli değil", now); return; }

        var rule = settings.Rule(email.Kind);
        var addresses = await CourseEmailContent.RecipientsAsync(db, email.WorkspaceId, student, rule.Audience, ct);
        if (addresses.Count == 0) { email.Skip("Alıcı e-postası yok", now); return; }
        var optedOut = await db.EmailOptOuts.AsNoTracking().Where(item => item.WorkspaceId == email.WorkspaceId
            && addresses.Contains(item.Email)).Select(item => item.Email).ToListAsync(ct);
        var recipients = addresses.Except(optedOut).ToArray();
        if (recipients.Length == 0) { email.Skip("Alıcılar e-postalardan çıktı", now); return; }

        foreach (var address in recipients)
            await publish.Publish(new CourseManagementEmailRequestedV1(email.Id, email.WorkspaceId, workspace.Name,
                    settings.ReplyToEmail, address, content.Subject, content.Title, content.Paragraphs, rule.Note,
                    CourseEmailLinks.UnsubscribeUrl(options, email.WorkspaceId, address)),
                context => context.MessageId = CourseEmailLinks.MessageId(email.Id, address), ct);
        email.MarkSent(content.Subject, recipients, now);
    }
}

public sealed record CourseEmailContentView(string Subject, string Title, IReadOnlyList<string> Paragraphs);

/// <summary>Turkish texts of the e-mails. Plain text; the notification service escapes and lays it out.</summary>
public static class CourseEmailContent
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static string When(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, CourseManagementTime.Turkey).ToString("d MMMM dddd HH:mm", Turkish);
    public static string Day(DateOnly day) => day.ToString("d MMMM yyyy", Turkish);
    public static string Money(decimal amount) => amount.ToString("N2", Turkish) + " TL";

    public static (DateTimeOffset From, DateTimeOffset To) NextWeek(DateTimeOffset now)
    {
        var local = CourseEmailCollector.TurkeyNow(now).Date;
        var daysToMonday = ((int)DayOfWeek.Monday - (int)local.DayOfWeek + 7) % 7;
        if (daysToMonday == 0) daysToMonday = 7;
        var monday = local.AddDays(daysToMonday);
        var nextMonday = monday.AddDays(7);
        // UTC: PostgreSQL stores instants and Npgsql refuses other offsets.
        return (new DateTimeOffset(monday, CourseManagementTime.Turkey.GetUtcOffset(monday)).ToUniversalTime(),
            new DateTimeOffset(nextMonday, CourseManagementTime.Turkey.GetUtcOffset(nextMonday)).ToUniversalTime());
    }

    public sealed record OpenInstallment(Guid Id, Guid StudentId, string Label, string CourseName, decimal Remaining, DateOnly DueOn);

    /// <summary>Planned installments (not per-lesson charges) with money still owed.</summary>
    public static async Task<List<OpenInstallment>> OpenInstallmentsAsync(CourseManagementDbContext db, Guid workspaceId,
        Func<IQueryable<Installment>, IQueryable<Installment>> filter, CancellationToken ct)
    {
        var reversed = db.Installments.Where(item => item.WorkspaceId == workspaceId && item.ReversalOfInstallmentId != null)
            .Select(item => item.ReversalOfInstallmentId!.Value);
        var rows = await (from installment in filter(db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && item.Amount > 0 && item.ReversalOfInstallmentId == null && item.SourceLessonId == null
                    && !reversed.Contains(item.Id)))
            join agreement in db.FeeAgreements.AsNoTracking() on installment.FeeAgreementId equals agreement.Id
            join course in db.Courses.AsNoTracking() on agreement.CourseId equals course.Id
            select new
            {
                installment.Id, installment.StudentId, installment.Label, CourseName = course.Name, installment.Amount, installment.DueOn,
                Paid = db.PaymentAllocations.Where(item => item.InstallmentId == installment.Id).Sum(item => (decimal?)item.Amount) ?? 0m
            }).ToListAsync(ct);
        return rows.Where(row => row.Amount - row.Paid > 0m)
            .Select(row => new OpenInstallment(row.Id, row.StudentId, row.Label, row.CourseName, row.Amount - row.Paid, row.DueOn)).ToList();
    }

    public static async Task<List<string>> RecipientsAsync(CourseManagementDbContext db, Guid workspaceId, StudentRecord student,
        CourseEmailAudience audience, CancellationToken ct)
    {
        var addresses = new List<string>();
        if (audience is CourseEmailAudience.Student or CourseEmailAudience.Both && !string.IsNullOrWhiteSpace(student.Email))
            addresses.Add(student.Email!);
        if (audience is CourseEmailAudience.Guardians or CourseEmailAudience.Both)
            addresses.AddRange(await (from link in db.StudentGuardianLinks.AsNoTracking()
                join guardian in db.Guardians.AsNoTracking() on link.GuardianContactId equals guardian.Id
                where link.WorkspaceId == workspaceId && link.StudentId == student.Id && link.IsActive && guardian.Email != null
                select guardian.Email!).ToListAsync(ct));
        return addresses.Select(address => address.Trim().ToLowerInvariant()).Where(address => address.Contains('@'))
            .Distinct().ToList();
    }

    public static async Task<CourseEmailContentView?> ComposeAsync(CourseManagementDbContext db, CourseEmail email,
        StudentRecord student, DateTimeOffset now, CancellationToken ct)
    {
        var name = student.DisplayName;
        var references = email.References.ToArray();
        switch (email.Kind)
        {
            case CourseEmailKind.Absence:
            {
                var lessons = await (from attendance in db.AttendanceRecords.AsNoTracking()
                    join lesson in db.LessonSessions.AsNoTracking() on attendance.LessonSessionId equals lesson.Id
                    join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                    where attendance.WorkspaceId == email.WorkspaceId && attendance.StudentId == student.Id
                        && Enumerable.Contains(references, attendance.LessonSessionId) && attendance.Status == LessonAttendanceStatus.Absent
                    orderby lesson.StartsAt
                    select new { course.Name, lesson.StartsAt }).ToListAsync(ct);
                if (lessons.Count == 0) return null;
                return new($"{name} derse gelmedi", $"{name} derse gelmedi",
                    [.. lessons.Select(lesson => $"{lesson.Name}: {When(lesson.StartsAt)} dersinde yok yazıldı."),
                     "Bir yanlışlık olduğunu düşünüyorsanız kurumla iletişime geçin."]);
            }
            case CourseEmailKind.HomeworkNotDone:
            {
                var items = await (from recipient in db.AssignmentRecipients.AsNoTracking()
                    join assignment in db.Assignments.AsNoTracking() on recipient.AssignmentId equals assignment.Id
                    join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
                    where recipient.WorkspaceId == email.WorkspaceId && recipient.StudentId == student.Id
                        && Enumerable.Contains(references, recipient.AssignmentId)
                        && (recipient.Completion == CourseAssignmentCompletion.NotDone || recipient.Completion == CourseAssignmentCompletion.Partial)
                    orderby assignment.DueAt
                    select new { CourseName = course.Name, assignment.Title, recipient.Completion, recipient.CompletionNote }).ToListAsync(ct);
                if (items.Count == 0) return null;
                return new($"{name}: ödev durumu", $"{name} ödevini tamamlamadı",
                    [.. items.Select(item => $"{item.CourseName}, {item.Title}: "
                        + (item.Completion == CourseAssignmentCompletion.Partial ? "eksik yapıldı" : "yapılmadı")
                        + (item.CompletionNote is null ? "." : $". Öğretmenin notu: {item.CompletionNote}"))]);
            }
            case CourseEmailKind.LessonChanged:
            {
                var lines = new List<string>();
                var lessons = await (from lesson in db.LessonSessions.AsNoTracking()
                    join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                    where lesson.WorkspaceId == email.WorkspaceId && Enumerable.Contains(references, lesson.Id)
                    orderby lesson.StartsAt
                    select new { course.Name, lesson.StartsAt, lesson.Status }).ToListAsync(ct);
                lines.AddRange(lessons.Where(lesson => lesson.StartsAt > now).Select(lesson => lesson.Status == LessonSessionStatus.Cancelled
                    ? $"{lesson.Name}: {When(lesson.StartsAt)} dersi iptal edildi."
                    : $"{lesson.Name}: ders yeni saatinde, {When(lesson.StartsAt)}."));
                var series = await (from item in db.LessonSeries.AsNoTracking()
                    join course in db.Courses.AsNoTracking() on item.CourseId equals course.Id
                    where item.WorkspaceId == email.WorkspaceId && Enumerable.Contains(references, item.Id)
                    select new { item.Id, course.Name }).ToListAsync(ct);
                foreach (var item in series)
                {
                    var upcoming = await db.LessonSessions.AsNoTracking().Where(lesson => lesson.SeriesId == item.Id
                            && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now)
                        .OrderBy(lesson => lesson.StartsAt).Select(lesson => lesson.StartsAt).Take(4).ToListAsync(ct);
                    lines.Add(upcoming.Count == 0
                        ? $"{item.Name}: kalan dersler iptal edildi."
                        : $"{item.Name}: ders programı değişti. Sıradaki dersler: {string.Join("; ", upcoming.Select(When))}.");
                }
                if (lines.Count == 0) return null;
                return new($"{name}: ders programında değişiklik", "Ders programında değişiklik", lines);
            }
            case CourseEmailKind.PaymentReceived:
            {
                var paymentId = references.FirstOrDefault();
                var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == paymentId
                    && item.WorkspaceId == email.WorkspaceId, ct);
                if (payment is null || await db.Payments.AnyAsync(item => item.ReversalOfPaymentId == paymentId, ct)) return null;
                return new($"{name}: ödemeniz alındı", "Ödemeniz alındı",
                    [$"{name} için {Day(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.ReceivedAt, CourseManagementTime.Turkey).DateTime))} tarihinde "
                     + $"{Money(payment.Amount)} ödeme alındı ({(payment.Method == PaymentMethod.Cash ? "nakit" : "havale/EFT")}).",
                     "Bu e-posta bilgilendirme amaçlıdır; resmi makbuz yerine geçmez."]);
            }
            case CourseEmailKind.InstallmentReminder:
            case CourseEmailKind.InstallmentOverdue:
            {
                var installmentId = references.FirstOrDefault();
                var open = (await OpenInstallmentsAsync(db, email.WorkspaceId, query => query.Where(item => item.Id == installmentId), ct))
                    .SingleOrDefault();
                if (open is null) return null;
                var line = $"{open.CourseName}, {open.Label}: {Money(open.Remaining)}, son ödeme günü {Day(open.DueOn)}.";
                return email.Kind == CourseEmailKind.InstallmentOverdue
                    ? new($"{name}: ödemesi geciken taksit", "Ödemesi geciken taksit",
                        [line, "Ödemeyi yaptıysanız bu e-postayı dikkate almayın."])
                    : new($"{name}: yaklaşan taksit", "Yaklaşan taksit", [line]);
            }
            case CourseEmailKind.WeeklySchedule:
            {
                var (weekStart, weekEnd) = NextWeek(email.CreatedAt);
                var lessons = await (from participant in db.LessonParticipants.AsNoTracking()
                    join lesson in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals lesson.Id
                    join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                    where participant.WorkspaceId == email.WorkspaceId && participant.StudentId == student.Id && participant.IsActive
                        && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt >= weekStart && lesson.StartsAt < weekEnd
                    orderby lesson.StartsAt
                    select new { course.Name, lesson.StartsAt }).ToListAsync(ct);
                if (lessons.Count == 0) return null;
                return new($"{name}: haftalık ders programı", "Bu haftanın dersleri",
                    lessons.Select(lesson => $"{When(lesson.StartsAt)}: {lesson.Name}").ToArray());
            }
            default:
                return null;
        }
    }

    /// <summary>Sample texts for the settings page preview and the test e-mail.</summary>
    public static CourseEmailContentView Sample(CourseEmailKind kind)
    {
        var lesson = When(new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.FromHours(3)));
        return kind switch
        {
            CourseEmailKind.Absence => new("Örnek Öğrenci derse gelmedi", "Örnek Öğrenci derse gelmedi",
                [$"Matematik: {lesson} dersinde yok yazıldı.", "Bir yanlışlık olduğunu düşünüyorsanız kurumla iletişime geçin."]),
            CourseEmailKind.HomeworkNotDone => new("Örnek Öğrenci: ödev durumu", "Örnek Öğrenci ödevini tamamlamadı",
                ["Matematik, Sayfa 40–45: yapılmadı. Öğretmenin notu: Defterini getirmemiş."]),
            CourseEmailKind.InstallmentReminder => new("Örnek Öğrenci: yaklaşan taksit", "Yaklaşan taksit",
                [$"Matematik, 2. taksit: {Money(2500)}, son ödeme günü {Day(new DateOnly(2026, 12, 1))}."]),
            CourseEmailKind.InstallmentOverdue => new("Örnek Öğrenci: ödemesi geciken taksit", "Ödemesi geciken taksit",
                [$"Matematik, 2. taksit: {Money(2500)}, son ödeme günü {Day(new DateOnly(2026, 11, 15))}.",
                 "Ödemeyi yaptıysanız bu e-postayı dikkate almayın."]),
            CourseEmailKind.LessonChanged => new("Örnek Öğrenci: ders programında değişiklik", "Ders programında değişiklik",
                [$"Matematik: {lesson} dersi iptal edildi."]),
            CourseEmailKind.WeeklySchedule => new("Örnek Öğrenci: haftalık ders programı", "Bu haftanın dersleri",
                [$"{lesson}: Matematik", $"{When(new DateTimeOffset(2026, 11, 26, 17, 0, 0, TimeSpan.FromHours(3)))}: Fen bilimleri"]),
            _ => new("Örnek Öğrenci: ödemeniz alındı", "Ödemeniz alındı",
                [$"Örnek Öğrenci için {Day(new DateOnly(2026, 11, 20))} tarihinde {Money(2500)} ödeme alındı (nakit).",
                 "Bu e-posta bilgilendirme amaçlıdır; resmi makbuz yerine geçmez."])
        };
    }
}

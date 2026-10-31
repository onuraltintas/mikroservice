using System.Globalization;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// Roadmap Y7: e-mails a linked parent the child's week and leaves the same note in the app.
/// V3 (parent panel): <see cref="CoachingParentWeeklySummaryV2"/> carries a Koçluk and a Hızlı okuma section, each optional;
/// one e-mail shows the sections present. <see cref="CoachingParentWeeklySummaryV1"/> (Coaching only) is still accepted for
/// messages published before the upgrade. Both use the same queue.
/// </summary>
public sealed class CoachingParentWeeklySummaryConsumer(
    ICoachingNotificationDispatcher dispatcher,
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions,
    INotificationChannelPolicy? channels = null) : IConsumer<CoachingParentWeeklySummaryV1>, IConsumer<CoachingParentWeeklySummaryV2>
{
    private const string Product = "Eduİvme";
    private const string Type = "ParentWeeklySummary";
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // A section's lines: plain text for the app, and HTML for the e-mail (only user-typed text needs encoding).
    private sealed record Section(string Heading, List<string> Text, List<string> Html);

    public Task Consume(ConsumeContext<CoachingParentWeeklySummaryV1> context)
    {
        var s = context.Message;
        var coaching = new CoachingParentWeeklyCoachingSection(s.StudyTasksPlanned, s.StudyTasksCompleted, s.AssignmentsDue,
            s.AssignmentsSubmitted, s.AssignmentsOverdue, s.SessionsHeld, s.SessionsAttended, s.SessionsUpcoming, s.LatestExamTitle, s.LatestExamScore);
        return SendAsync(context, new CoachingParentWeeklySummaryV2(s.SummaryId, s.ParentUserId, s.ParentEmail, s.ParentName,
            s.StudentUserId, s.StudentName, s.WeekStart, s.WeekEnd, coaching, null));
    }

    public Task Consume(ConsumeContext<CoachingParentWeeklySummaryV2> context) => SendAsync(context, context.Message);

    private async Task SendAsync<T>(ConsumeContext<T> context, CoachingParentWeeklySummaryV2 s) where T : class
    {
        if (s.SummaryId == Guid.Empty || s.ParentUserId == Guid.Empty) throw new InvalidOperationException("A parent summary requires ids.");
        var sections = new List<Section>();
        if (s.Coaching is { } coaching) sections.Add(Coaching(coaching));
        if (s.SpeedReading is { } reading) sections.Add(SpeedReading(reading));
        if (sections.Count == 0) return;

        var week = $"{s.WeekStart.ToString("dd.MM.yyyy", Turkish)} – {s.WeekEnd.ToString("dd.MM.yyyy", Turkish)}";
        var title = $"{s.StudentName} için haftalık özet";
        var message = string.Join(" ", sections.Select(x => $"{x.Heading}: {string.Join(" ", x.Text)}"));
        await dispatcher.SendAsync(s.SummaryId, [s.ParentUserId], title, message, Type, s.StudentUserId.ToString(), context.CancellationToken);

        if (string.IsNullOrWhiteSpace(s.ParentEmail)) return;
        // Roadmap O9: the parent may turn the summary e-mail off in the notification preferences.
        if (channels is not null && !(await channels.PlanAsync([s.ParentUserId], Type, context.CancellationToken))[s.ParentUserId].Email) return;
        var link = $"{publicAppUrlOptions.Value.ResolveBaseUrl(null, "coaching")}/coaching-portal/parent/children/{s.StudentUserId}";
        var body = $"<h1>{Product}</h1><p>Merhaba {EmailContent.Html(s.ParentName)},</p>"
            + $"<p><strong>{EmailContent.Html(s.StudentName)}</strong> için {week} haftasının özeti:</p>"
            + string.Concat(sections.Select(x => $"<h2>{x.Heading}</h2><ul>{string.Concat(x.Html.Select(line => $"<li>{line}</li>"))}</ul>"))
            + $"<p><a href=\"{link}\">Çocuğunun sayfasını aç</a></p>"
            + "<p>Bu e-postayı haftalık özet tercihin açık olduğu için alıyorsun.</p>";
        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(CoachingParentWeeklySummaryConsumer), s.ParentEmail.Trim(),
            $"{Product}: {EmailContent.SubjectText(title)}", body, s.ParentUserId, context.CancellationToken);
    }

    private static Section Coaching(CoachingParentWeeklyCoachingSection s)
    {
        var lines = new List<string>
        {
            s.StudyTasksPlanned == 0 ? "Bu hafta planlanmış çalışma yoktu." : $"Planlanan {s.StudyTasksPlanned} çalışmanın {s.StudyTasksCompleted} tanesi tamamlandı.",
            s.AssignmentsDue == 0 ? "Bu hafta teslim tarihi gelen ödev yoktu."
                : $"Teslim tarihi gelen {s.AssignmentsDue} ödevden {s.AssignmentsSubmitted} tanesi teslim edildi"
                  + (s.AssignmentsOverdue > 0 ? $", {s.AssignmentsOverdue} tanesi gecikti." : "."),
            s.SessionsHeld == 0 ? "Bu hafta koçluk seansı yapılmadı." : $"{s.SessionsHeld} koçluk seansının {s.SessionsAttended} tanesine katıldı.",
            s.SessionsUpcoming == 0 ? "Önümüzdeki 7 gün için planlanmış seans yok." : $"Önümüzdeki 7 günde {s.SessionsUpcoming} seans planlandı."
        };
        // Only the exam title is typed by people; the rest is our own wording and needs no encoding.
        var html = lines.ToList();
        if (s.LatestExamTitle is not null)
        {
            var score = s.LatestExamScore?.ToString("0.##", Turkish) ?? "puan girilmedi";
            lines.Add($"Son deneme: {s.LatestExamTitle} — {score} puan.");
            html.Add($"Son deneme: {EmailContent.Html(s.LatestExamTitle)} — {score} puan.");
        }
        return new("Koçluk", lines, html);
    }

    /// <summary>Only measured values are shown; a missing value (null) leaves its line out rather than showing 0.</summary>
    private static Section SpeedReading(CoachingParentWeeklySpeedReadingSection s)
    {
        var lines = new List<string>();
        if (s.Minutes == 0 && s.ActiveDays == 0 && s.ExercisesCompleted == 0) lines.Add("Bu hafta hızlı okuma çalışması yapılmadı.");
        else lines.Add($"{s.Minutes} dakika, {s.ActiveDays} gün çalıştı; {s.ExercisesCompleted} egzersiz tamamladı.");
        if (s.LatestWpm is { } wpm)
            lines.Add($"Son okuma hızı: {Math.Round(wpm).ToString("0", Turkish)} kelime/dk"
                + (s.WpmChange is { } change && Math.Round(change) != 0 ? $" ({(change > 0 ? "+" : "−")}{Math.Abs(Math.Round(change)).ToString("0", Turkish)})." : "."));
        if (s.AverageComprehension is { } comprehension)
            lines.Add($"Ortalama anlama: %{Math.Round(comprehension).ToString("0", Turkish)}.");
        if (s.ProgramDay is { } day && s.ProgramTotalDays is { } total && total > 0)
            lines.Add($"{total} günlük programın {day}. günü.");
        if (s.BadgesEarnedThisWeek > 0) lines.Add($"Bu hafta {s.BadgesEarnedThisWeek} yeni rozet kazandı.");
        return new("Hızlı okuma", lines, lines.ToList());
    }
}

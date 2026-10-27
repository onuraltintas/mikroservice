using System.Globalization;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>Roadmap Y7: e-mails a linked parent the child's week and leaves the same note in the app.</summary>
public sealed class CoachingParentWeeklySummaryConsumer(
    ICoachingNotificationDispatcher dispatcher,
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<CoachingParentWeeklySummaryV1>
{
    private const string Product = "Eduİvme Koçluk";
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task Consume(ConsumeContext<CoachingParentWeeklySummaryV1> context)
    {
        var s = context.Message;
        if (s.SummaryId == Guid.Empty || s.ParentUserId == Guid.Empty) throw new InvalidOperationException("A parent summary requires ids.");
        var week = $"{s.WeekStart.ToString("dd.MM.yyyy", Turkish)} – {s.WeekEnd.ToString("dd.MM.yyyy", Turkish)}";
        var lines = new List<string>
        {
            s.StudyTasksPlanned == 0 ? "Bu hafta planlanmış çalışma yoktu." : $"Planlanan {s.StudyTasksPlanned} çalışmanın {s.StudyTasksCompleted} tanesi tamamlandı.",
            s.AssignmentsDue == 0 ? "Bu hafta teslim tarihi gelen ödev yoktu."
                : $"Teslim tarihi gelen {s.AssignmentsDue} ödevden {s.AssignmentsSubmitted} tanesi teslim edildi"
                  + (s.AssignmentsOverdue > 0 ? $", {s.AssignmentsOverdue} tanesi gecikti." : "."),
            s.SessionsHeld == 0 ? "Bu hafta koçluk seansı yapılmadı." : $"{s.SessionsHeld} koçluk seansının {s.SessionsAttended} tanesine katıldı.",
            s.SessionsUpcoming == 0 ? "Önümüzdeki 7 gün için planlanmış seans yok." : $"Önümüzdeki 7 günde {s.SessionsUpcoming} seans planlandı."
        };
        // Only the exam title and names are typed by people; the rest is our own wording and needs no encoding.
        var htmlLines = lines.ToList();
        if (s.LatestExamTitle is not null)
        {
            var score = s.LatestExamScore?.ToString("0.##", Turkish) ?? "puan girilmedi";
            lines.Add($"Son deneme: {s.LatestExamTitle} — {score} puan.");
            htmlLines.Add($"Son deneme: {EmailContent.Html(s.LatestExamTitle)} — {score} puan.");
        }
        var title = $"{s.StudentName} için haftalık özet";
        var message = string.Join(" ", lines);
        await dispatcher.SendAsync(s.SummaryId, [s.ParentUserId], title, message, "ParentWeeklySummary", s.StudentUserId.ToString(), context.CancellationToken);

        if (string.IsNullOrWhiteSpace(s.ParentEmail)) return;
        var link = $"{publicAppUrlOptions.Value.ResolveBaseUrl(null, "coaching")}/coaching-portal/parent";
        var body = $"<h1>{Product}</h1><p>Merhaba {EmailContent.Html(s.ParentName)},</p>"
            + $"<p><strong>{EmailContent.Html(s.StudentName)}</strong> için {week} haftasının özeti:</p><ul>"
            + string.Concat(htmlLines.Select(line => $"<li>{line}</li>"))
            + $"</ul><p><a href=\"{link}\">Veli panelini aç</a></p>"
            + "<p>Bu e-postayı haftalık özet tercihin açık olduğu için alıyorsun.</p>";
        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(CoachingParentWeeklySummaryConsumer), s.ParentEmail.Trim(),
            $"{Product}: {EmailContent.SubjectText(title)}", body, s.ParentUserId, context.CancellationToken);
    }
}

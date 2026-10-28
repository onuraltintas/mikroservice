namespace Notification.Application.Services;

/// <summary>
/// Roadmap O8: notification types grouped into the categories users choose from. Important coaching events
/// (sessions, assignments and their due reminders, messages, fees, account links) are e-mailed by default.
/// </summary>
public static class NotificationCategories
{
    /// <param name="OwnEmail">The category's consumer writes its own e-mail (it reads the choice); the generic channel must not add one.</param>
    /// <param name="EmailEditable">False for billing notices that are always e-mailed.</param>
    /// <param name="TextTypes">Roadmap O9: the types that may also go by SMS / WhatsApp when the user opts in.</param>
    public sealed record Category(string Key, string Label, bool EmailByDefault, IReadOnlyList<string> Types,
        bool OwnEmail = false, bool EmailEditable = true, IReadOnlyList<string>? TextTypes = null)
    {
        public bool TextAvailable => TextTypes is { Count: > 0 };
        public bool AllowsText(string type) => TextTypes?.Contains(type) == true;
    }

    public static IReadOnlyList<Category> All { get; } =
    [
        new("Sessions", "Koçluk seansları (planlama, değişiklik, iptal, yaklaşan seans)", true,
            ["SessionScheduled", "SessionUpdated", "SessionCancelled", "SessionReminder"], TextTypes: ["SessionReminder"]),
        new("Assignments", "Ödevler (yeni, değişen, iptal, teslim ve değerlendirme)", true,
            ["AssignmentCreated", "AssignmentUpdated", "AssignmentCancelled", "AssignmentSubmitted", "AssignmentGraded"]),
        new("AssignmentReminders", "Ödev teslim hatırlatması (teslimden bir gün önce)", true, ["AssignmentDueReminder"]),
        new("Messages", "Yeni mesajlar", true, ["CoachingMessage"]),
        new("Fees", "Ödeme hatırlatmaları", true, ["FeeOverdueReminder"]),
        new("Connections", "Davet ve öğretmen bağlantıları", true, ["InvitationAccepted", "TeacherLinkEnded"]),
        new("Exams", "Deneme ve sınav sonuçları", false, ["ExamResultAdded", "ExamResultUpdated", "ExamUpdated"]),
        new("Goals", "Akademik hedefler", false, ["GoalCreated", "GoalUpdated"]),
        new("StudyPlan", "Çalışma planı hatırlatmaları", false, ["StudyPlanReminder", "StudyPlanOverdueReminder"]),
        new("ParentSummary", "Haftalık veli özeti", true, ["ParentWeeklySummary"], OwnEmail: true, TextTypes: ["ParentWeeklySummary"]),
        new("Subscription", "Abonelik bitiş hatırlatmaları (e-posta her zaman gönderilir)", true, ["SubscriptionExpiring", "SubscriptionExpired"],
            OwnEmail: true, EmailEditable: false, TextTypes: ["SubscriptionExpiring", "SubscriptionExpired"]),
    ];

    private static readonly Dictionary<string, Category> ByType =
        All.SelectMany(category => category.Types.Select(type => (type, category))).ToDictionary(x => x.type, x => x.category, StringComparer.Ordinal);
    private static readonly Dictionary<string, Category> ByKey = All.ToDictionary(x => x.Key, StringComparer.Ordinal);

    public static Category? ForType(string? type) => type is not null && ByType.TryGetValue(type, out var category) ? category : null;
    public static Category? ForKey(string? key) => key is not null && ByKey.TryGetValue(key, out var category) ? category : null;
}

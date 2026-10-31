using Notification.Application.Configuration;

namespace Notification.Application.Services;

/// <summary>Roadmap O8: notification wording that names the student and the assignment.</summary>
public static class CoachingNotificationTexts
{
    public sealed record Text(string Title, string Message);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static Text AssignmentSubmitted(string? studentName, string? title, DateTime submittedAt)
    {
        var name = Clean(studentName) ?? "Bir öğrenci"; var assignment = Clean(title);
        return new(assignment is null ? "Ödev teslim edildi" : $"Ödev teslim edildi: {assignment}",
            assignment is null ? $"{name} ödevini teslim etti: {TurkeyTime.Format(submittedAt)}"
                : $"{name} \"{assignment}\" ödevini teslim etti: {TurkeyTime.Format(submittedAt)}");
    }

    public static Text AssignmentGraded(string? title, decimal score, string? feedback)
    {
        var assignment = Clean(title); var note = Clean(feedback) is { } text ? $" Geri bildirim: {text}" : string.Empty;
        return new(assignment is null ? "Ödev değerlendirildi" : $"Ödev değerlendirildi: {assignment}", $"Ödev puanın: {score:0.##}.{note}");
    }

    public static Text AssignmentDue(string? title, DateTime dueAt)
    {
        var assignment = Clean(title);
        // Coaching audit K3: an assignment given less than a day ahead gets the reminder too, so the title gives no count.
        return new("Ödev teslim tarihi yaklaşıyor",
            assignment is null ? $"Teslim etmediğin bir ödevin son tarihi {TurkeyTime.Format(dueAt)}."
                : $"\"{assignment}\" ödevinin son teslim tarihi {TurkeyTime.Format(dueAt)}. Henüz teslim etmedin.");
    }

    public static Text InvitationAccepted(string? inviteeName, string? invitationType)
    {
        var name = Clean(inviteeName) ?? "Davet ettiğin kişi";
        var role = invitationType == "TeacherToInstitution" ? "öğretmen" : "öğrenci";
        return new("Davetin kabul edildi", $"{name} davetini kabul etti ve {role} olarak katıldı.");
    }
}

using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.Consumers;

/// <summary>
/// The one-time "welcome" goal for a new Coaching student. Account creation and e-mail confirmation can
/// both announce the same student (and redeliver), so creation is idempotent per student.
/// </summary>
internal static class WelcomeGoal
{
    public const string Title = "Profilini Tamamla ve İlk Koçunu Bul";

    public static async Task<bool> EnsureAsync(IAcademicGoalRepository goals, IUnitOfWork unitOfWork, Guid studentId, CancellationToken ct)
    {
        if (await goals.HasSystemGoalAsync(studentId, Title, ct)) return false;
        var goal = AcademicGoal.Create(studentId: studentId, title: Title, category: GoalCategory.StudyHabits, setByTeacherId: null);
        goal.UpdateDetails(description: "Platforma hoş geldin! İlk adım olarak profil bilgilerini doldur ve çalışma planını kur. Öğretmenin ya da kurumun seni davet ederse daveti bildirimlerinden kabul edebilirsin.");
        goal.SetTarget(targetDate: DateTime.UtcNow.AddDays(7), targetScore: null);
        await goals.AddAsync(goal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

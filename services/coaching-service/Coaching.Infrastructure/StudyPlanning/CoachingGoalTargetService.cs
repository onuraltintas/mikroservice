using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingGoalTargetService(CoachingDbContext db, ICoachingAccessPolicy access,
    ICoachingEventPublisher events) : IGoalTargetService
{
    public async Task<GoalTargetView?> GetAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        var goal = await db.AcademicGoals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken);
        return goal is null ? null : View(goal);
    }

    public async Task<GoalTargetView> ReplaceAsync(Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 0 || request.TargetSchoolId == Guid.Empty || request.TargetUniversityProgramId == Guid.Empty
            || (request.TargetSchoolId.HasValue && request.TargetUniversityProgramId.HasValue))
            throw new ArgumentException("Tek bir okul veya üniversite programı seçin.");
        var goal = await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Hedef bulunamadı.");
        if (goal.SetByTeacherId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğretmenin oluşturduğu hedefi yalnız yetkili öğretmen düzenleyebilir.");
        if (goal.Version != request.ExpectedVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Hedef değişti. Güncel kaydı yükleyip tekrar deneyin.");
        if (request.TargetSchoolId is { } schoolId
            && !await db.TargetSchools.AnyAsync(x => x.Id == schoolId && x.IsActive, cancellationToken))
            throw new ArgumentException("Seçilen okul artık kullanılamıyor.");
        if (request.TargetUniversityProgramId is { } programId
            && !await db.TargetUniversityPrograms.AnyAsync(x => x.Id == programId && x.IsActive, cancellationToken))
            throw new ArgumentException("Seçilen üniversite programı artık kullanılamıyor.");
        if (goal.TargetSchoolId == request.TargetSchoolId && goal.TargetUniversityProgramId == request.TargetUniversityProgramId)
            return View(goal);
        goal.SetCatalogTarget(request.TargetUniversityProgramId, request.TargetSchoolId);
        // The registered publisher uses the same scoped Coaching EF bus outbox.
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return View(goal);
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }
    private static GoalTargetView View(AcademicGoal goal) => new(goal.Id, goal.Version, goal.TargetUniversityProgramId,
        goal.TargetSchoolId, !goal.SetByTeacherId.HasValue);
}

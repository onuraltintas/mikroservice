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
        return goal is null ? null : await ViewAsync(goal, cancellationToken);
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
            return await ViewAsync(goal, cancellationToken);
        goal.SetCatalogTarget(request.TargetUniversityProgramId, request.TargetSchoolId);
        // The registered publisher uses the same scoped Coaching EF bus outbox.
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ViewAsync(goal, cancellationToken);
    }

    public async Task<GoalTargetView> ReplaceScoreAsync(Guid goalId, GoalScoreTargetUpdate request, CancellationToken cancellationToken = default)
    {
        var studentId = RequireStudent();
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 0) throw new ArgumentException("A valid version is required.");
        var goal = await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Hedef bulunamadı.");
        if (goal.SetByTeacherId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğretmenin oluşturduğu hedefi öğrenci değiştiremez.");
        if (goal.Version != request.ExpectedVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Hedef değişti. Güncel kaydı yükleyip tekrar deneyin.");
        goal.SetScoreTarget(request.TargetScore, request.MaxScore, request.ExamType);
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ViewAsync(goal, cancellationToken);
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }
    private async Task<GoalTargetView> ViewAsync(AcademicGoal goal, CancellationToken cancellationToken)
    {
        // Existing links remain readable even when the catalog entry becomes inactive.
        GoalCatalogTargetView? target = null;
        if (goal.TargetSchoolId is { } schoolId)
            target = await db.TargetSchools.AsNoTracking().Where(x => x.Id == schoolId)
                .Select(x => new GoalCatalogTargetView(x.Name, x.City + " / " + x.District, x.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
        else if (goal.TargetUniversityProgramId is { } programId)
            target = await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == programId)
                .Select(x => new GoalCatalogTargetView(x.UniversityName + " — " + x.Name, x.ScoreType ?? "", x.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
        return new(goal.Id, goal.Version, goal.TargetUniversityProgramId, goal.TargetSchoolId,
            !goal.SetByTeacherId.HasValue, target,
            new(goal.TargetScore, goal.TargetMaxScore, goal.TargetExamType, goal.TargetSubject));
    }
}

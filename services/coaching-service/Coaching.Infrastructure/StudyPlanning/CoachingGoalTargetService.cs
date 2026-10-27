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
    ICoachingEventPublisher events, CoachingStudyPlanAccess? staffAccess = null) : IGoalTargetService
{
    public Task<GoalTargetView?> GetAsync(Guid goalId, CancellationToken cancellationToken = default) => GetCoreAsync(goalId, null, cancellationToken);
    public Task<GoalTargetView?> GetForStudentAsync(Guid studentId, Guid goalId, CancellationToken cancellationToken = default) => GetCoreAsync(goalId, studentId, cancellationToken);
    private async Task<GoalTargetView?> GetCoreAsync(Guid goalId, Guid? staffStudentId, CancellationToken cancellationToken)
    {
        var studentId = await RequireAsync(staffStudentId, false, cancellationToken);
        var goal = await db.AcademicGoals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken);
        return goal is null ? null : await ViewAsync(goal, cancellationToken,
            staffStudentId.HasValue ? await CanTeacherEditAsync(goal, cancellationToken) : null);
    }

    public Task<GoalTargetView> ReplaceAsync(Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default) => ReplaceCoreAsync(goalId, request, null, cancellationToken);
    public Task<GoalTargetView> ReplaceForStudentAsync(Guid studentId, Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default) => ReplaceCoreAsync(goalId, request, studentId, cancellationToken);
    private async Task<GoalTargetView> ReplaceCoreAsync(Guid goalId, GoalTargetUpdate request, Guid? staffStudentId, CancellationToken cancellationToken)
    {
        var studentId = await RequireAsync(staffStudentId, true, cancellationToken);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 0 || request.TargetSchoolId == Guid.Empty || request.TargetUniversityProgramId == Guid.Empty
            || (request.TargetSchoolId.HasValue && request.TargetUniversityProgramId.HasValue))
            throw new ArgumentException("Tek bir okul veya üniversite programı seçin.");
        var goal = await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Hedef bulunamadı.");
        if (staffStudentId.HasValue ? !IsTeacherOwner(goal) : goal.SetByTeacherId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğretmenin oluşturduğu hedefi yalnız yetkili öğretmen düzenleyebilir.");
        if (goal.Version != request.ExpectedVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Hedef değişti. Güncel kaydı yükleyip tekrar deneyin.");
        if (request.TargetSchoolId is { } schoolId
            && !await db.TargetSchools.AnyAsync(x => x.Id == schoolId && x.IsActive, cancellationToken))
            throw new ArgumentException("Seçilen okul artık kullanılamıyor.");
        if (request.TargetUniversityProgramId is { } programId
            && !await db.TargetUniversityPrograms.AnyAsync(x => x.Id == programId && x.IsActive, cancellationToken))
            throw new ArgumentException("Seçilen üniversite programı artık kullanılamıyor.");
        if (await db.Set<StudentPrimaryGoal>().AnyAsync(x => x.StudentId == studentId && x.GoalId == goalId, cancellationToken))
        {
            var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == studentId, cancellationToken);
            if (context?.Purpose == LearningPurpose.Lgs && request.TargetUniversityProgramId.HasValue
                || context?.Purpose == LearningPurpose.Yks && request.TargetSchoolId.HasValue)
                throw new ArgumentException("Ana hedef profilindeki sınavla eşleşmeli.");
            if (context?.Purpose == LearningPurpose.Yks && request.TargetUniversityProgramId is { } selectedProgram)
            {
                var score = await db.TargetUniversityPrograms.Where(x => x.Id == selectedProgram).Select(x => x.ScoreType).SingleAsync(cancellationToken);
                if (!YksScoreTypes.ProgramFitsProfile(context.PrimaryScoreType, score))
                    throw new ArgumentException("Programın puan türü profilindeki alanla eşleşmeli.");
            }
        }
        foreach (var metric in goal.NetTargets)
            await ValidateNetCatalogAsync(request.TargetSchoolId, request.TargetUniversityProgramId, metric.ExamCode, cancellationToken);
        if (goal.TargetSchoolId == request.TargetSchoolId && goal.TargetUniversityProgramId == request.TargetUniversityProgramId)
            return await ViewAsync(goal, cancellationToken, staffStudentId.HasValue ? true : null);
        goal.SetCatalogTarget(request.TargetUniversityProgramId, request.TargetSchoolId);
        // The registered publisher uses the same scoped Coaching EF bus outbox.
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ViewAsync(goal, cancellationToken, staffStudentId.HasValue ? true : null);
    }

    public Task<GoalTargetView> ReplaceScoreAsync(Guid goalId, GoalScoreTargetUpdate request, CancellationToken cancellationToken = default) => ReplaceScoreCoreAsync(goalId, request, null, cancellationToken);
    public Task<GoalTargetView> ReplaceScoreForStudentAsync(Guid studentId, Guid goalId, GoalScoreTargetUpdate request, CancellationToken cancellationToken = default) => ReplaceScoreCoreAsync(goalId, request, studentId, cancellationToken);
    private async Task<GoalTargetView> ReplaceScoreCoreAsync(Guid goalId, GoalScoreTargetUpdate request, Guid? staffStudentId, CancellationToken cancellationToken)
    {
        var studentId = await RequireAsync(staffStudentId, true, cancellationToken);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 0) throw new ArgumentException("A valid version is required.");
        var goal = await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Hedef bulunamadı.");
        if (staffStudentId.HasValue ? !IsTeacherOwner(goal) : goal.SetByTeacherId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğretmenin oluşturduğu hedefi öğrenci değiştiremez.");
        if (goal.Version != request.ExpectedVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Hedef değişti. Güncel kaydı yükleyip tekrar deneyin.");
        goal.SetScoreTarget(request.TargetScore, request.MaxScore, request.ExamType);
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ViewAsync(goal, cancellationToken, staffStudentId.HasValue ? true : null);
    }

    private bool IsTeacherOwner(AcademicGoal goal) => access.CurrentUserId is { } actor && access.IsCurrentTeacher(actor) && goal.SetByTeacherId == actor;
    private async Task<bool> CanTeacherEditAsync(AcademicGoal goal, CancellationToken ct)
    {
        if (!IsTeacherOwner(goal) || staffAccess is null) return false;
        try { await staffAccess.RequireTeacherWriteAsync(goal.StudentId, ct); return true; }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal)) { return false; }
    }
    private async Task<Guid> RequireAsync(Guid? studentId, bool write, CancellationToken ct)
    {
        if (!studentId.HasValue) return RequireStudent();
        if (staffAccess is null) throw new BusinessRuleException("Authorization.Forbidden", "Personel kapsamı doğrulanamadı.");
        if (write) await staffAccess.RequireTeacherWriteAsync(studentId.Value, ct);
        else await staffAccess.RequireAsync(studentId.Value, ct);
        return studentId.Value;
    }

    private Guid RequireStudent()
    {
        if (access.CurrentUserId is not { } studentId || !access.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return studentId;
    }
    private async Task<GoalTargetView> ViewAsync(AcademicGoal goal, CancellationToken cancellationToken, bool? canEdit = null)
    {
        // Existing links remain readable even when the catalog entry becomes inactive.
        GoalCatalogTargetView? target = null;
        if (goal.TargetSchoolId is { } schoolId)
            target = await db.TargetSchools.AsNoTracking().Where(x => x.Id == schoolId)
                .Select(x => new GoalCatalogTargetView(x.Name, x.City + " / " + x.District, x.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
        else if (goal.TargetUniversityProgramId is { } programId)
            target = await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == programId)
                .Select(x => new GoalCatalogTargetView(x.UniversityName + " — " + x.Name,
                    (x.FacultyName ?? "") + " · " + (x.ProgramType ?? "") + " · " + (x.ScoreType ?? ""), x.IsActive, x.ScoreType))
                .SingleOrDefaultAsync(cancellationToken);
        return new(goal.Id, goal.Version, goal.TargetUniversityProgramId, goal.TargetSchoolId,
            canEdit ?? !goal.SetByTeacherId.HasValue, target,
            new(goal.TargetScore, goal.TargetMaxScore, goal.TargetExamType, goal.TargetSubject), new(goal.TargetPracticeExamCode, goal.TargetNet), goal.NetTargets.Select(x => new GoalNetTargetView(x.ExamCode, x.TargetNet)).ToArray());
    }

    public Task<GoalTargetView> ReplaceNetAsync(Guid goalId, GoalNetTargetUpdate request, CancellationToken ct = default)
        => ReplaceNetCoreAsync(goalId, request, null, ct);
    public Task<GoalTargetView> ReplaceNetForStudentAsync(Guid studentId, Guid goalId, GoalNetTargetUpdate request, CancellationToken ct = default)
        => ReplaceNetCoreAsync(goalId, request, studentId, ct);
    private async Task<GoalTargetView> ReplaceNetCoreAsync(Guid goalId, GoalNetTargetUpdate request, Guid? staffStudentId, CancellationToken ct)
    {
        var student = await RequireAsync(staffStudentId, true, ct); ArgumentNullException.ThrowIfNull(request);
        var goal = await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == student, ct)
            ?? throw new KeyNotFoundException("Hedef bulunamadı.");
        if (staffStudentId.HasValue ? !IsTeacherOwner(goal) : goal.SetByTeacherId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Bu hedefi yalnız oluşturan kişi düzenleyebilir.");
        if (request.ExpectedVersion < 0 || goal.Version != request.ExpectedVersion)
            throw new BusinessRuleException("StudyPlanning.Conflict", "Hedef değişti. Yeniden yükleyin.");
        if (request.ExamCode is { } code && await db.Set<StudentPrimaryGoal>().AnyAsync(x => x.StudentId == student && x.GoalId == goalId, ct))
        {
            var context = await db.StudentLearningContexts.AsNoTracking().SingleOrDefaultAsync(x => x.StudentUserId == student, ct);
            var advanced = YksScoreTypes.AdvancedNetExam(context?.PrimaryScoreType);
            if (context?.Purpose == LearningPurpose.Lgs && code != "LGS" || context?.Purpose == LearningPurpose.Yks && code != "TYT" && code != advanced)
                throw new ArgumentException("Ana hedefin net kapsamı profilindeki sınavla eşleşmeli.");
        }
        await ValidateNetCatalogAsync(goal.TargetSchoolId, goal.TargetUniversityProgramId, request.ExamCode, ct);
        goal.SetNetTarget(request.ExamCode, request.TargetNet);
        await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), ct);
        await db.SaveChangesAsync(ct); return await ViewAsync(goal, ct, staffStudentId.HasValue ? true : null);
    }
    private async Task ValidateNetCatalogAsync(Guid? schoolId, Guid? programId, string? code, CancellationToken ct)
    {
        if (code is null) return;
        if (schoolId.HasValue && code != "LGS") throw new ArgumentException("LGS okul hedefi için LGS net kapsamını seçin.");
        if (programId is { } id)
        {
            var scoreType = await db.TargetUniversityPrograms.Where(x=>x.Id==id).Select(x=>x.ScoreType).SingleAsync(ct);
            var expected = YksScoreTypes.AdvancedNetExam(scoreType);
            if (code != "TYT" && code != expected) throw new ArgumentException("Net kapsamı programın puan türüyle eşleşmeli. Katalog hedefini değiştirirken önce uyumsuz net hedefini kaldırın.");
        }
    }
}

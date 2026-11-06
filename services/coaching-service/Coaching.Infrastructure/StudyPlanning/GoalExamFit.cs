using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// Paket D (#9): which parts of a goal still fit the student's exam and field. A school fits LGS; a programme fits YKS when its
/// score type fits the field; target scores fit when their score type belongs to the exam (the wizard's ScoreTargetTypes); net
/// targets fit the exam's practice scopes (and the kept school or programme). The same rules the wizard and Hedefim enforce
/// when a target is chosen.
/// </summary>
internal static class GoalExamFit
{
    /// <summary>
    /// The study settings changed the exam or the field: the student's own primary goal keeps only what still fits, and the
    /// change is published as any goal change. A teacher's goal is never changed here (Hedefim then shows the mismatch).
    /// </summary>
    public static async Task AlignPrimaryAsync(CoachingDbContext db, Guid student, StudentLearningContext context, ICoachingEventPublisher? events, CancellationToken ct)
    {
        if (!LearningPurposes.IsExamPreparation(context.Purpose)) return;
        var goalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == student).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        var goal = goalId is null ? null : await db.AcademicGoals.SingleOrDefaultAsync(x => x.Id == goalId && x.StudentId == student && x.SetByTeacherId == null, ct);
        if (goal is null || !await ApplyAsync(db, goal, context, true, ct)) return;
        if (events is not null) await events.PublishAsync(new GoalUpdatedEvent(goal.Id, goal.StudentId, goal.SetByTeacherId, goal.Title), ct);
    }

    /// <summary>True when every part of the goal fits the exam (nothing would be removed).</summary>
    public static async Task<bool> FitsAsync(CoachingDbContext db, AcademicGoal goal, StudentLearningContext context, CancellationToken ct) =>
        !LearningPurposes.IsExamPreparation(context.Purpose) || !await ApplyAsync(db, goal, context, false, ct);

    /// <summary>Returns whether something does not fit; with <paramref name="apply"/> it is removed from the goal.</summary>
    private static async Task<bool> ApplyAsync(CoachingDbContext db, AcademicGoal goal, StudentLearningContext context, bool apply, CancellationToken ct)
    {
        var purpose = context.Purpose;
        var examType = CoachingStudentOnboardingService.ExamTypeOf(purpose);
        var schoolFits = goal.TargetSchoolId is null || purpose == LearningPurpose.Lgs;
        string? programScore = null;
        if (goal.TargetUniversityProgramId is { } programId)
            programScore = await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == programId).Select(x => x.ScoreType).SingleOrDefaultAsync(ct);
        var programFits = goal.TargetUniversityProgramId is null
            || purpose == LearningPurpose.Yks && YksScoreTypes.ProgramFitsProfile(context.PrimaryScoreType, programScore);

        var allowedScores = CoachingStudentOnboardingService.ScoreTargetTypes(context);
        var scores = goal.ScoreTargets;
        var keptScores = scores.Where(x => allowedScores.Contains(x.ScoreType)).ToArray();
        var scoresFit = scores.Count == 0 ? LegacyScoreFits(goal, examType) : keptScores.Length == scores.Count && goal.TargetExamType == examType;

        IEnumerable<string> nets = LearningPurposes.NetExamCodes(purpose, context.PrimaryScoreType);
        if (goal.TargetSchoolId is not null && schoolFits) nets = nets.Intersect(["LGS"]);
        if (goal.TargetUniversityProgramId is not null && programFits) nets = nets.Intersect(["TYT", YksScoreTypes.AdvancedNetExam(programScore)]);
        var allowedNets = nets.ToArray();
        var keptNets = goal.NetTargets.Where(x => allowedNets.Contains(x.ExamCode)).ToArray();
        var netsFit = keptNets.Length == goal.NetTargets.Count;

        if (schoolFits && programFits && scoresFit && netsFit) return false;
        if (!apply) return true;
        if (!schoolFits || !programFits)
            goal.SetCatalogTarget(programFits ? goal.TargetUniversityProgramId : null, schoolFits ? goal.TargetSchoolId : null);
        if (!scoresFit)
        {
            if (keptScores.Length > 0) goal.SetScoreTargets(keptScores, examType);
            else goal.SetScoreTargets([], null);
        }
        if (!netsFit)
        {
            goal.SetNetTarget(null, null);
            foreach (var keep in keptNets) goal.SetNetTarget(keep.ExamCode, keep.TargetNet);
        }
        return true;
    }

    /// <summary>An older single target score fits when it names no exam, a non-exam type, or this exam.</summary>
    private static bool LegacyScoreFits(AcademicGoal goal, ExamType examType) =>
        goal.TargetScore is null || goal.TargetExamType is not (ExamType.LGS or ExamType.YKS or ExamType.AGS or ExamType.YDS or ExamType.YOKDIL)
        || goal.TargetExamType == examType;
}

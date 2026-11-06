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
        !LearningPurposes.IsExamPreparation(context.Purpose) || !(await PlanAsync(db, goal, context, ct)).Removes;

    /// <summary>
    /// E4 #26: the parts of the goal the exam would remove, as the student reads them ("Ankara Üniversitesi · Bilgisayar
    /// Mühendisliği", "YKS Sayısal hedef puanın (420)", "AYT · Sayısal net hedefin (60)"); nothing is changed.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DescribeRemovalsAsync(CoachingDbContext db, AcademicGoal goal, StudentLearningContext context, CancellationToken ct)
    {
        if (!LearningPurposes.IsExamPreparation(context.Purpose)) return [];
        var plan = await PlanAsync(db, goal, context, ct);
        if (!plan.Removes) return [];
        var parts = new List<string>();
        if (!plan.SchoolFits) parts.Add(await SchoolNameAsync(db, goal.TargetSchoolId!.Value, ct));
        if (!plan.ProgramFits) parts.Add(await ProgramNameAsync(db, goal.TargetUniversityProgramId!.Value, ct));
        if (!plan.ScoresFit)
        {
            if (goal.ScoreTargets.Count == 0) parts.Add($"Hedef puanın ({Number(goal.TargetScore!.Value)})");
            else parts.AddRange(goal.ScoreTargets.Where(x => !plan.KeptScores.Contains(x)).Select(x => $"{ScoreName(x.ScoreType)} hedef puanın ({Number(x.Score)})"));
        }
        parts.AddRange(goal.NetTargets.Where(x => !plan.KeptNets.Contains(x)).Select(x => $"{NetName(x.ExamCode)} net hedefin ({Number(x.TargetNet)})"));
        return parts;
    }

    /// <summary>E4 #23/#26: every part of the goal as the student reads it (the catalog target first, then the scores).</summary>
    public static async Task<string?> DescribeAsync(CoachingDbContext db, AcademicGoal goal, CancellationToken ct)
    {
        var parts = new List<string>();
        if (goal.TargetSchoolId is { } school) parts.Add(await SchoolNameAsync(db, school, ct));
        if (goal.TargetUniversityProgramId is { } program) parts.Add(await ProgramNameAsync(db, program, ct));
        if (goal.ScoreTargets.Count > 0) parts.AddRange(goal.ScoreTargets.Select(x => $"{ScoreName(x.ScoreType)} hedef puanın ({Number(x.Score)})"));
        else if (goal.TargetScore is { } legacy) parts.Add($"Hedef puanın ({Number(legacy)})");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Returns whether something does not fit; with <paramref name="apply"/> it is removed from the goal.</summary>
    private static async Task<bool> ApplyAsync(CoachingDbContext db, AcademicGoal goal, StudentLearningContext context, bool apply, CancellationToken ct)
    {
        var plan = await PlanAsync(db, goal, context, ct);
        if (!plan.Removes) return false;
        if (!apply) return true;
        if (!plan.SchoolFits || !plan.ProgramFits)
            goal.SetCatalogTarget(plan.ProgramFits ? goal.TargetUniversityProgramId : null, plan.SchoolFits ? goal.TargetSchoolId : null);
        if (!plan.ScoresFit)
        {
            if (plan.KeptScores.Length > 0) goal.SetScoreTargets(plan.KeptScores, plan.ExamType);
            else goal.SetScoreTargets([], null);
        }
        if (!plan.NetsFit)
        {
            var kept = plan.KeptNets.Select(x => (x.ExamCode, x.TargetNet)).ToArray();
            goal.SetNetTarget(null, null);
            foreach (var (exam, net) in kept) goal.SetNetTarget(exam, net);
        }
        return true;
    }

    private sealed record Plan(bool SchoolFits, bool ProgramFits, bool ScoresFit, bool NetsFit, AcademicScoreTarget[] KeptScores,
        AcademicNetTarget[] KeptNets, ExamType ExamType)
    {
        public bool Removes => !(SchoolFits && ProgramFits && ScoresFit && NetsFit);
    }

    private static async Task<Plan> PlanAsync(CoachingDbContext db, AcademicGoal goal, StudentLearningContext context, CancellationToken ct)
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
        return new(schoolFits, programFits, scoresFit, keptNets.Length == goal.NetTargets.Count, keptScores, keptNets, examType);
    }

    private static async Task<string> SchoolNameAsync(CoachingDbContext db, Guid id, CancellationToken ct) =>
        await db.TargetSchools.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).SingleOrDefaultAsync(ct) ?? "Hedef okulun";
    private static async Task<string> ProgramNameAsync(CoachingDbContext db, Guid id, CancellationToken ct) =>
        await db.TargetUniversityPrograms.AsNoTracking().Where(x => x.Id == id).Select(x => x.UniversityName + " · " + x.Name).SingleOrDefaultAsync(ct) ?? "Hedef bölümün";
    private static string ScoreName(string type) => CoachingStudentOnboardingService.ScoreTypeName(type);
    private static string NetName(string code) => PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == code)?.Name ?? code;
    private static string Number(decimal value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>An older single target score fits when it names no exam, a non-exam type, or this exam.</summary>
    private static bool LegacyScoreFits(AcademicGoal goal, ExamType examType) =>
        goal.TargetScore is null || goal.TargetExamType is not (ExamType.LGS or ExamType.YKS or ExamType.AGS or ExamType.YDS or ExamType.YOKDIL)
        || goal.TargetExamType == examType;
}

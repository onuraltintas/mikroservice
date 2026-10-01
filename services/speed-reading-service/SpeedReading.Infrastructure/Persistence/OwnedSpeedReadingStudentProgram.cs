using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Analytics;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Institutions;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudentProgram(OwnedSpeedReadingDbContext db, ISpeedReadingAssessment assessment,
    ISpeedReadingTeacherAccess teacherAccess) : ISpeedReadingStudentProgram
{
    public async Task<NextStudentProgramRecommendation?> GetNextProgramRecommendationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user is required.", nameof(userId));
        if (await db.StudentProgramProgresses.AsNoTracking().AnyAsync(item => item.UserId == userId && item.IsActive, cancellationToken))
            return null;
        var progress = await db.StudentProgramProgresses.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AssignedDate).ThenByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress?.CompletedDate is null) return null;
        var post = await db.AssessmentAttempts.AsNoTracking().Where(item => item.StudentId == userId
            && item.ProgramProgressId == progress.Id && item.Phase == AssessmentAttemptPhase.PostTraining
            && item.Status == AssessmentAttemptStatus.Completed && !item.IsSkipped)
            .OrderByDescending(item => item.CompletedAt).ThenBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (post is null) return null;
        var comparison = await assessment.GetComparisonAsync(userId, cancellationToken);
        var measurement = comparison.Attempts.FirstOrDefault(item => item.AttemptId == post.Id);
        if (measurement?.AverageWpm is not > 0 || measurement.AverageComprehension is not (>= 0 and <= 100))
            return null;
        var age = await db.UserProfiles.AsNoTracking().Where(item => item.UserId == userId && item.IsActive)
            .Select(item => item.AgeGroupConfigurationId).SingleOrDefaultAsync(cancellationToken);
        age ??= await db.ProgramTemplates.AsNoTracking().Where(item => item.Id == progress.ProgramTemplateId)
            .Select(item => (Guid?)item.TargetAgeGroupConfigurationId).SingleOrDefaultAsync(cancellationToken);
        var score = measurement.AverageComprehension.Value;
        var template = await db.ProgramTemplates.AsNoTracking().Where(item => item.IsActive && !item.IsDeleted
            && !item.IsAssessment && item.ProgramType == 0 && (item.ExamType == null || item.ExamType == "")
            && item.TargetAgeGroupConfigurationId == age && item.MinAssessmentScore <= score && item.MaxAssessmentScore >= score)
            .OrderByDescending(item => item.MinAssessmentScore).ThenBy(item => item.DisplayOrder).ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null) return null;
        var managed = await db.TeacherStudentAssignments.AsNoTracking().AnyAsync(item => item.StudentUserId == userId && item.IsActive, cancellationToken)
            || await db.InstitutionMemberships.AsNoTracking().AnyAsync(item => item.UserId == userId && item.IsActive
                && item.Role == SpeedReadingInstitutionMemberRole.Student, cancellationToken);
        return new NextStudentProgramRecommendation(progress.Id, post.Id, template.Id, template.Name, template.TotalDays, managed);
    }

    public async Task<StudentProgramInfo?> GetMyProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await GetRows(userId)
            .Where(item => item.Progress.IsActive)
            .OrderByDescending(item => item.Progress.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToInfo(row);
    }

    public async Task<IReadOnlyList<StudentProgramInfo>> GetMyProgramsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await GetRows(userId)
            .OrderByDescending(item => item.Progress.IsActive)
            .ThenByDescending(item => item.Progress.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInfo).ToList();
    }

    public Task<StartStudentProgramResult> StartProgramAsync(Guid userId, Guid templateId, CancellationToken cancellationToken) =>
        StartProgramCoreAsync(userId, userId, templateId, null, null, cancellationToken);

    public Task<StartStudentProgramResult> ConfirmNextProgramAsync(Guid userId, ConfirmNextStudentProgramRequest request,
        CancellationToken cancellationToken) => StartProgramCoreAsync(userId, userId, request.TemplateId, request, null, cancellationToken);

    public Task<StartStudentProgramResult> ApproveNextProgramAsync(Guid actorId, Guid userId, ConfirmNextStudentProgramRequest request,
        Guid? institutionId, CancellationToken cancellationToken)
    {
        if (actorId == userId) throw new UnauthorizedAccessException("A student cannot approve their own managed program.");
        return StartProgramCoreAsync(actorId, userId, request.TemplateId, request, institutionId, cancellationToken);
    }

    private async Task<StartStudentProgramResult> StartProgramCoreAsync(
        Guid actorId,
        Guid userId,
        Guid templateId,
        ConfirmNextStudentProgramRequest? confirmation,
        Guid? institutionId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || userId == Guid.Empty || templateId == Guid.Empty
            || (confirmation is not null && (confirmation.SourceProgressId == Guid.Empty || confirmation.AssessmentAttemptId == Guid.Empty)))
            throw new ArgumentException("A valid user and program template are required.");

        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && item.IsActive && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");

        var attempt = 0;
        return await OwnedSpeedReadingProgramAssignmentLock.ExecuteAsync(
            db,
            async () =>
            {
                if (attempt++ > 0)
                    db.ChangeTracker.Clear();
                await using var transaction = await OwnedSpeedReadingProgramAssignmentLock.AcquireAsync(
                    db,
                    userId,
                    cancellationToken);

                if (actorId != userId)
                {
                    var allowed = institutionId.HasValue
                        ? await teacherAccess.CanReadInstitutionStudentAsync(actorId, institutionId.Value, userId,
                            cancellationToken: cancellationToken)
                        : await teacherAccess.CanReadStudentAsync(actorId, userId, cancellationToken: cancellationToken);
                    if (!allowed) throw new UnauthorizedAccessException("You cannot approve this student's program.");
                }

                var activePrograms = await db.StudentProgramProgresses
                    .Where(item => item.UserId == userId
                        && item.IsActive
                        && item.CompletedDate == null)
                    .OrderByDescending(item => item.CreatedAt)
                    .ToListAsync(cancellationToken);
                if (activePrograms.Count == 1 && activePrograms[0].ProgramTemplateId == templateId)
                    return new StartStudentProgramResult(true, activePrograms[0].Id, template.Name,
                        "Bu program zaten aktif; mevcut ilerlemeniz korundu.");
                if (activePrograms.Count > 0)
                    throw new BusinessRuleException("Program.ActiveTrainingExists",
                        "Yeni bir programa başlamadan önce aktif programınızı tamamlamalısınız.");
                if (await db.StudentProgramProgresses.AsNoTracking().AnyAsync(item => item.UserId == userId, cancellationToken))
                {
                    var recommendation = await GetNextProgramRecommendationAsync(userId, cancellationToken);
                    if (recommendation is null || recommendation.TemplateId != templateId
                        || (confirmation is not null && (confirmation.SourceProgressId != recommendation.SourceProgressId
                            || confirmation.AssessmentAttemptId != recommendation.AssessmentAttemptId)))
                        throw new BusinessRuleException("Program.RecommendationChanged",
                            "Geçerli program önerisi bulunamadı veya değişti. Eğitim sonrası ölçümünüzü kontrol edin.");
                    if (recommendation.RequiresStaffApproval && actorId == userId)
                        throw new BusinessRuleException("Program.StaffApprovalRequired",
                            "Yeni programınız için öğretmeninizin veya kurum yöneticinizin onayı gerekir.");
                }
                else if (confirmation is not null)
                    throw new BusinessRuleException("Program.RecommendationChanged", "Tamamlanmış program önerisi bulunamadı.");
                var now = DateTime.UtcNow;

                var progress = StudentProgramProgress.Start(
                    Guid.NewGuid(),
                    userId,
                    template,
                    0,
                    0,
                    actorId,
                    now);
                progress.SetSchedule(
                    await OwnedSpeedReadingProgramSchedule.BuildAsync(db, template, null, cancellationToken),
                    actorId,
                    now);
                db.StudentProgramProgresses.Add(progress);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);

                return new StartStudentProgramResult(
                    true,
                    progress.Id,
                    template.Name,
                    $"'{template.Name}' programına başladınız!");
            });
    }

    private IQueryable<StudentProgramRow> GetRows(Guid userId) =>
        from progress in db.StudentProgramProgresses.AsNoTracking()
        join template in db.ProgramTemplates.AsNoTracking()
            on progress.ProgramTemplateId equals template.Id
        join ageGroup in db.AgeGroupConfigurations.AsNoTracking()
            on template.TargetAgeGroupConfigurationId equals ageGroup.Id into ageGroups
        from ageGroup in ageGroups.DefaultIfEmpty()
        where progress.UserId == userId
        select new StudentProgramRow(progress, template, ageGroup == null ? "Bilinmiyor" : ageGroup.DisplayName);

    private static StudentProgramInfo ToInfo(StudentProgramRow row)
    {
        var programTypeName = row.Template.ProgramType == 1 ? "Sınav Hazırlık" : "Standart Program";
        var visibleDay = row.Progress.IsActive && !row.Progress.CompletedDate.HasValue
            ? Math.Min(((row.Progress.CurrentWeek - 1) * 7) + row.Progress.CurrentDay,
                SpeedReading.Application.DailyProgress.SpeedReadingDailyProgressRules.GetCalendarAvailableDay(row.Progress.AssignedDate, DateTime.UtcNow))
            : ((row.Progress.CurrentWeek - 1) * 7) + row.Progress.CurrentDay;
        var (week, day) = SpeedReading.Application.DailyProgress.SpeedReadingDailyProgressRules.GetWeekAndDay(Math.Max(visibleDay, 1));
        return new StudentProgramInfo(
            row.Progress.Id,
            row.Progress.ProgramTemplateId,
            row.Template.Name,
            row.Template.Description,
            row.Template.ProgramType,
            programTypeName,
            row.Template.ExamType,
            row.Template.TargetAgeGroupConfigurationId,
            row.AgeGroupName,
            row.Template.MinAssessmentScore,
            row.Template.MaxAssessmentScore,
            week,
            day,
            row.Progress.CurrentDifficultyLevel,
            row.Template.MaxDifficultyLevel,
            row.Progress.DaysCompleted,
            row.Progress.ExercisesCompleted,
            row.Progress.AverageSuccessRate,
            row.Progress.CurrentStreak,
            row.Progress.LongestStreak,
            row.Progress.AssignedDate,
            row.Progress.LastCompletionDate,
            row.Progress.IsActive,
            row.Progress.CompletedDate);
    }

    private sealed record StudentProgramRow(
        StudentProgramProgress Progress,
        ProgramTemplate Template,
        string AgeGroupName);
}

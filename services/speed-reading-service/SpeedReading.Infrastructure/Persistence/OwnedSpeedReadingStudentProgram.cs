using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Analytics;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Institutions;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Programs;
using System.Text.Json;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudentProgram(OwnedSpeedReadingDbContext db, ISpeedReadingAssessment assessment,
    ISpeedReadingTeacherAccess teacherAccess, ISpeedReadingLevelCatalog levelCatalog) : ISpeedReadingStudentProgram
{
    public OwnedSpeedReadingStudentProgram(OwnedSpeedReadingDbContext db, ISpeedReadingAssessment assessment,
        ISpeedReadingTeacherAccess teacherAccess) : this(db, assessment, teacherAccess, new DefaultSpeedReadingLevelCatalog()) { }
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
        var comparison = await assessment.GetProgramComparisonAsync(userId, progress.Id, cancellationToken);
        var measurement = comparison.Attempts.FirstOrDefault(item => item.AttemptId == post.Id);
        if (measurement?.AverageWpm is not > 0 || measurement.PlacementComprehensionScore is not (>= 0 and <= 100))
            return null;
        var age = await db.UserProfiles.AsNoTracking().Where(item => item.UserId == userId && item.IsActive)
            .Select(item => item.AgeGroupConfigurationId).SingleOrDefaultAsync(cancellationToken);
        age ??= await db.ProgramTemplates.AsNoTracking().Where(item => item.Id == progress.ProgramTemplateId)
            .Select(item => (Guid?)item.TargetAgeGroupConfigurationId).SingleOrDefaultAsync(cancellationToken);
        var score = measurement.PlacementComprehensionScore.Value;
        var catalog = await levelCatalog.GetByVersionAsync(post.LevelCatalogVersion, cancellationToken)
            ?? throw new InvalidOperationException("Ölçümün seviye kataloğu bulunamadı.");
        var measuredLevel = SpeedReadingAssessmentMeasurementRules.CalculateLevel(measurement.AverageWpm.Value, score, catalog.Definitions);
        var trainingLevel = Math.Clamp(measuredLevel, 1, 5);
        var candidates = await db.ProgramTemplates.AsNoTracking().Where(item => item.IsActive && !item.IsDeleted
            && !item.IsAssessment && item.ProgramType == 0 && (item.ExamType == null || item.ExamType == "")
            && item.TargetAgeGroupConfigurationId == age)
            .OrderByDescending(item => item.MinAssessmentScore).ThenBy(item => item.DisplayOrder).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var levelTemplates = candidates.Where(item => PlacementLevel(item).HasValue).ToList();
        var template = levelTemplates.Count > 0
            ? levelTemplates.FirstOrDefault(item => PlacementLevel(item) == trainingLevel)
            : candidates.FirstOrDefault(item => item.MinAssessmentScore <= score && item.MaxAssessmentScore >= score);
        if (template is null) return null;
        var higher = levelTemplates.FirstOrDefault(item => PlacementLevel(item) == trainingLevel + 1);
        var managed = await db.TeacherStudentAssignments.AsNoTracking().AnyAsync(item => item.StudentUserId == userId && item.IsActive, cancellationToken)
            || await db.InstitutionMemberships.AsNoTracking().AnyAsync(item => item.UserId == userId && item.IsActive
                && item.Role == SpeedReadingInstitutionMemberRole.Student, cancellationToken);
        var requested = await db.ProgramContinuations.AsNoTracking().SingleOrDefaultAsync(item => item.SourceProgressId == progress.Id
            && item.StudentId == userId && item.ConfirmedAt == null && item.AssessmentAttemptId == post.Id, cancellationToken);
        return new NextStudentProgramRecommendation(progress.Id, post.Id, template.Id, template.Name, template.TotalDays, managed)
        {
            RecommendedTrainingLevel = PlacementLevel(template) ?? template.InitialDifficultyLevel,
            MeasuredLevel = measuredLevel, RuleVersion = $"{catalog.Version}:training-1-5-v1",
            AverageWpm = measurement.AverageWpm.Value, Comprehension = score,
            WpmDelta = measurement.WpmDeltaFromBaseline, ComprehensionDelta = measurement.ComprehensionDeltaFromBaseline,
            TrainingSuccessRate = progress.AverageSuccessRate,
            Reason = "Öneri yaş grubunuz ve son ölçümde hız ile anlamanın birlikte sınırlandırdığı seviyeye dayanır. Başlangıca göre değişim ve eğitim başarısı bilgilendirme amaçlıdır; tek başına seviye yükseltmez.",
            HigherOption = higher is null ? null : new NextProgramOption(higher.Id, higher.Name, higher.TotalDays, trainingLevel + 1),
            RequestedTemplateId = requested?.TemplateId, RequestedAt = requested?.RequestedAt
        };
    }

    private static int? PlacementLevel(ProgramTemplate template)
    {
        using var json = JsonDocument.Parse(template.WeeklyPatternJson);
        return json.RootElement.TryGetProperty("placementLevel", out var level) && level.TryGetInt32(out var value) ? value : null;
    }

    private static void ValidateSelection(NextStudentProgramRecommendation? recommendation, ConfirmNextStudentProgramRequest request)
    {
        if (recommendation is null || request.SourceProgressId != recommendation.SourceProgressId
            || request.AssessmentAttemptId != recommendation.AssessmentAttemptId
            || (request.TemplateId != recommendation.TemplateId && request.TemplateId != recommendation.HigherOption?.TemplateId))
            throw new BusinessRuleException("Program.RecommendationChanged", "Öneri değişti. Güncel öneriyi yeniden yükleyin.");
        if (request.TemplateId != recommendation.TemplateId && !request.OverrideAcknowledged)
            throw new BusinessRuleException("Program.OverrideAcknowledgementRequired", "Üst seviye seçimi için uyarıyı açıkça onaylayın.");
    }

    public async Task RequestNextProgramAsync(Guid userId, ConfirmNextStudentProgramRequest request, CancellationToken cancellationToken)
    {
        var attempt = 0;
        await OwnedSpeedReadingProgramAssignmentLock.ExecuteAsync(db, async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await OwnedSpeedReadingProgramAssignmentLock.AcquireAsync(db, userId, cancellationToken);
            var recommendation = await GetNextProgramRecommendationAsync(userId, cancellationToken);
            ValidateSelection(recommendation, request);
            if (!recommendation!.RequiresStaffApproval)
                throw new BusinessRuleException("Program.StaffApprovalNotRequired", "Önerinizi doğrudan onaylayabilirsiniz.");
            await SaveDecisionAsync(userId, recommendation, request, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private async Task<ProgramContinuation> SaveDecisionAsync(Guid userId, NextStudentProgramRecommendation recommendation,
        ConfirmNextStudentProgramRequest request, CancellationToken cancellationToken)
    {
        var existing = await db.ProgramContinuations.SingleOrDefaultAsync(item => item.SourceProgressId == request.SourceProgressId, cancellationToken);
        if (existing?.ConfirmedAt is not null) throw new BusinessRuleException("Program.AlreadyContinued", "Bu eğitim için geçiş zaten tamamlandı.");
        if (existing is not null)
        {
            existing.ChangeSelection(request.AssessmentAttemptId, request.TemplateId, recommendation.RecommendedTrainingLevel,
                recommendation.MeasuredLevel, recommendation.RuleVersion, request.OverrideAcknowledged, DateTime.UtcNow);
            return existing;
        }
        var decision = ProgramContinuation.Request(request.SourceProgressId, userId, request.AssessmentAttemptId, request.TemplateId,
            recommendation.RecommendedTrainingLevel, recommendation.MeasuredLevel, recommendation.RuleVersion, request.OverrideAcknowledged, DateTime.UtcNow);
        db.ProgramContinuations.Add(decision);
        return decision;
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

    public Task<StartStudentProgramResult> StartStaffTrainingAsync(Guid userId, Guid templateId, CancellationToken cancellationToken) =>
        StartProgramCoreAsync(userId, userId, templateId, null, null, cancellationToken, isStaffTraining: true);

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
        CancellationToken cancellationToken,
        bool isStaffTraining = false)
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
                {
                    if (activePrograms[0].IsStaffTraining != isStaffTraining)
                        throw new BusinessRuleException("Program.ModeMismatch", "Aktif program farklı bir kullanım akışına ait.");
                    if (confirmation is not null && !await db.ProgramContinuations.AnyAsync(item => item.StudentId == userId
                        && item.SourceProgressId == confirmation.SourceProgressId && item.AssessmentAttemptId == confirmation.AssessmentAttemptId
                        && item.TemplateId == templateId && item.NewProgressId == activePrograms[0].Id, cancellationToken))
                        throw new BusinessRuleException("Program.RecommendationChanged", "Bu onay aktif programa ait değil.");
                    return new StartStudentProgramResult(true, activePrograms[0].Id, template.Name,
                        "Bu program zaten aktif; mevcut ilerlemeniz korundu.");
                }
                if (activePrograms.Count > 0)
                    throw new BusinessRuleException("Program.ActiveTrainingExists",
                        "Yeni bir programa başlamadan önce aktif programınızı tamamlamalısınız.");
                NextStudentProgramRecommendation? approvedRecommendation = null;
                if (isStaffTraining)
                {
                    if (template.IsAssessment)
                        throw new BusinessRuleException("Program.TrainingRequired", "Seviye tespitini eğitim programı olarak başlatamazsınız.");
                }
                else if (await db.StudentProgramProgresses.AsNoTracking().AnyAsync(item => item.UserId == userId, cancellationToken))
                {
                    var recommendation = await GetNextProgramRecommendationAsync(userId, cancellationToken);
                    if (recommendation is null || (recommendation.TemplateId != templateId && recommendation.HigherOption?.TemplateId != templateId)
                        || (confirmation is not null && (confirmation.SourceProgressId != recommendation.SourceProgressId
                            || confirmation.AssessmentAttemptId != recommendation.AssessmentAttemptId)))
                        throw new BusinessRuleException("Program.RecommendationChanged",
                            "Geçerli program önerisi bulunamadı veya değişti. Eğitim sonrası ölçümünüzü kontrol edin.");
                    if (recommendation.RequiresStaffApproval && actorId == userId)
                        throw new BusinessRuleException("Program.StaffApprovalRequired",
                            "Yeni programınız için öğretmeninizin veya kurum yöneticinizin onayı gerekir.");
                    if (confirmation is null)
                        throw new BusinessRuleException("Program.ExplicitConfirmationRequired",
                            "Yeni programa başlamak için güncel öneriyi açıkça onaylamalısınız.");
                    ValidateSelection(recommendation, confirmation);
                    approvedRecommendation = recommendation;
                }
                else if (confirmation is not null)
                    throw new BusinessRuleException("Program.RecommendationChanged", "Tamamlanmış program önerisi bulunamadı.");
                else
                    throw new BusinessRuleException("Program.AssessmentPlacementRequired",
                        "İlk programınız seviye tespitinden sonra otomatik atanır. Önce seviye tespitini tamamlayın.");
                var now = DateTime.UtcNow;

                var progress = StudentProgramProgress.Start(
                    Guid.NewGuid(),
                    userId,
                    template,
                    0,
                    0,
                    actorId,
                    now,
                    isStaffTraining);
                progress.SetSchedule(
                    await OwnedSpeedReadingProgramSchedule.BuildAsync(db, template, null, cancellationToken),
                    actorId,
                    now);
                db.StudentProgramProgresses.Add(progress);
                if (approvedRecommendation is not null && confirmation is not null)
                {
                    var decision = await SaveDecisionAsync(userId, approvedRecommendation, confirmation, cancellationToken);
                    decision.Confirm(actorId, progress.Id, now);
                }
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
        select new StudentProgramRow
        {
            Progress = progress,
            Template = template,
            AgeGroupName = ageGroup == null ? "Bilinmiyor" : ageGroup.DisplayName
        };

    private static StudentProgramInfo ToInfo(StudentProgramRow row)
    {
        var programTypeName = row.Template.ProgramType == 1 ? "Sınav Hazırlık" : "Standart Program";
        var visibleDay = row.Progress.IsActive && !row.Progress.CompletedDate.HasValue
            ? Math.Min(((row.Progress.CurrentWeek - 1) * 7) + row.Progress.CurrentDay,
                SpeedReading.Application.DailyProgress.SpeedReadingDailyProgressRules.GetAvailableDay(row.Progress.AssignedDate, DateTime.UtcNow, row.Progress.IsStaffTraining))
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

    private sealed record StudentProgramRow
    {
        public required StudentProgramProgress Progress { get; init; }
        public required ProgramTemplate Template { get; init; }
        public required string AgeGroupName { get; init; }
    }
}

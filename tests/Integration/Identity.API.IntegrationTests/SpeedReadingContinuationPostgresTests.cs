using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Persistence.Migrations;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingContinuationPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Historical_calculation_rechecks_latest_program_after_waiting_for_assignment_lock()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var db = new OwnedSpeedReadingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid(); var now = DateTime.UtcNow;
            var age = AgeGroupConfiguration.Create(Guid.NewGuid(), "teen", "Genç", 13, 16, 100, 200, 300, 70, 15, 1, 1, true, null, user, now);
            db.AgeGroupConfigurations.Add(age);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Training", "", age.Id, 0, 100, "{}", 1, 0, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
            db.ProgramTemplates.Add(template);
            var source = StudentProgramProgress.Import(Guid.NewGuid(), user, template.Id, now.AddDays(-60), 1, 1, 1, 28, 100,
                now.AddDays(-1), false, now.AddDays(-1), 65, 1, 1, now.AddDays(-60), null, null, null);
            db.StudentProgramProgresses.Add(source);
            var post = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining, "post", "tr", age.Id, 3, now, null);
            post.BindToProgram(source.Id); db.AssessmentAttempts.Add(post);
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "focus"); db.ExerciseTypes.Add(type);
            for (var index = 0; index < 3; index++)
            {
                var exercise = Exercise.Create($"Fixation {index}", "Fixation", "{}", 1, user, type.Id); db.Exercises.Add(exercise);
                db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), post.Id, exercise.Id, null, "focus", index + 1, "{}", now, null));
                var session = ExerciseSession.Start(user, exercise.Id, null, 1, now, null, assessmentAttemptId: post.Id);
                session.Complete(now.AddSeconds(1)); db.ExerciseSessions.Add(session);
                db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user, exercise.Id, null,
                    80, 10, 150, 55, 0, 80, now, isMeasured: true, isAssessmentMode: true, assessmentAttemptId: post.Id));
            }
            var profile = SpeedReading.Domain.Profiles.SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), user, now);
            profile.ApplyAssessment(2, 200, 70, user, now); db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
            var observer = new LockObserver();
            await using var calculatingDb = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql(postgres.ConnectionString).AddInterceptors(observer).Options);
            var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
            var catalog = Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLevelCatalog")!, calculatingDb);
            var service = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, calculatingDb, catalog)!;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var bytes = user.ToByteArray();
            var key = BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var calculation = service.CalculateAsync(user, new AssessmentCalculationRequest([], post.Id), cancellation.Token);
            await observer.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(15));
            db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now.AddSeconds(2)));
            profile.ApplyAssessment(5, 450, 90, user, now.AddSeconds(2));
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            await calculation;
            db.ChangeTracker.Clear();
            var saved = await db.UserProfiles.SingleAsync();
            Assert.Equal(5, saved.CurrentLevel); Assert.Equal(450, saved.TargetWPM); Assert.Equal(90, saved.TargetComprehension);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class LockObserver : DbCommandInterceptor
    {
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) Waiting.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Continuation_migration_selection_and_historical_followup_preserve_cycles(bool managed)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var migration = new AddProgramContinuationDecisions();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(migration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            foreach (var command in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            Assert.False(db.Database.HasPendingModelChanges());
            var user = Guid.NewGuid(); var teacher = Guid.NewGuid(); var now = DateTime.UtcNow;
            var age = AgeGroupConfiguration.Create(Guid.NewGuid(), "teen", "Genç", 13, 16, 100, 200, 300, 70, 15, 1, 1, true, null, user, now);
            db.AgeGroupConfigurations.Add(age);
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "focus");
            db.ExerciseTypes.Add(type);
            var exercises = Enumerable.Range(0, 3).Select(index => Exercise.Create($"Fixation {index}", "Fixation", "{}", 1,
                user, type.Id, isAssessmentOnly: true)).ToArray();
            db.Exercises.AddRange(exercises);
            db.Exercises.Add(Exercise.Create("Normal Fixation", "Fixation", "{}", 1, user, type.Id));
            var templates = Enumerable.Range(1, 5).Select(level => ProgramTemplate.Import(Guid.NewGuid(), $"Level {level}", "", age.Id, 0, 100,
                $"{{\"placementLevel\":{level},\"week1\":{{\"day1\":[{{\"Type\":\"Fixation\",\"Count\":1,\"Difficulty\":1}}]}}}}",
                level, 0, 5, 1, 1, true, level, 0, null, false, now, null, null, null)).ToArray();
            db.ProgramTemplates.AddRange(templates);
            db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "", age.Id, 0, 100,
                JsonSerializer.Serialize(new { week1 = exercises.Select(exercise => new { exerciseId = exercise.Id }) }),
                1, 0, 5, 1, 1, true, 0, 0, null, true, now, null, null, null));
            var source = StudentProgramProgress.Import(Guid.NewGuid(), user, templates[0].Id, now.AddDays(-60),
                1, 1, 1, 28, 182, now.AddDays(-25), false, now.AddDays(-25), 65, 1, 1, now.AddDays(-60), null, null, null);
            db.StudentProgramProgresses.Add(source);
            var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline, "baseline", "tr", age.Id, 3, now.AddDays(-65), null);
            baseline.Complete(now.AddDays(-65));
            var post = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining, "post", "tr", age.Id, 3, now.AddDays(-20), null);
            post.BindToProgram(source.Id); post.Complete(now.AddDays(-20));
            db.AssessmentAttempts.AddRange(baseline, post);
            foreach (var exercise in exercises)
            {
                db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), baseline.Id, exercise.Id,
                    null, "focus", Array.IndexOf(exercises, exercise) + 1, "{}", now.AddDays(-65), null));
                var baselineSession = ExerciseSession.Start(user, exercise.Id, null, 1, now.AddDays(-65), null, assessmentAttemptId: baseline.Id);
                baselineSession.Complete(now.AddDays(-65).AddSeconds(1)); db.ExerciseSessions.Add(baselineSession);
                db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), baselineSession.Id, user, exercise.Id, null,
                    80, 10, 150, 55, 0, 80, now.AddDays(-65), isMeasured: true, isAssessmentMode: true, assessmentAttemptId: baseline.Id));
                var session = ExerciseSession.Start(user, exercise.Id, null, 1, now.AddDays(-20), null, assessmentAttemptId: post.Id);
                session.Complete(now.AddDays(-20).AddSeconds(1));
                db.ExerciseSessions.Add(session);
                db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user, exercise.Id, null,
                    80, 10, 150, 55, 0, 80, now.AddDays(-20), isMeasured: true, isAssessmentMode: true, assessmentAttemptId: post.Id));
            }
            if (managed) db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(null, teacher, user, user, now));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
            var catalog = (ISpeedReadingLevelCatalog)Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingLevelCatalog")!, db)!;
            var assessment = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, db, catalog)!;
            var access = DispatchProxy.Create<ISpeedReadingTeacherAccess, AccessProxy>();
            ((AccessProxy)access).Teacher = teacher; ((AccessProxy)access).Student = user;
            var programs = (ISpeedReadingStudentProgram)Activator.CreateInstance(assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, assessment, access, catalog)!;
            var profile = SpeedReading.Domain.Profiles.SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), user, now);
            profile.ApplyAssessment(5, 450, 90, user, now); db.UserProfiles.Add(profile); await db.SaveChangesAsync();
            await assessment.CalculateAsync(user, new AssessmentCalculationRequest([], baseline.Id), CancellationToken.None);
            await assessment.CalculateAsync(user, new AssessmentCalculationRequest([], baseline.Id), CancellationToken.None);
            await assessment.SkipAsync(user, CancellationToken.None);
            await assessment.SkipAsync(user, CancellationToken.None);
            Assert.Equal(1, await db.StudentProgramProgresses.CountAsync());
            Assert.False(await db.StudentProgramProgresses.AnyAsync(item => item.IsActive));
            Assert.Equal(5, profile.CurrentLevel); Assert.Equal(450, profile.TargetWPM);
            var recommendation = await programs.GetNextProgramRecommendationAsync(user, CancellationToken.None);
            Assert.Equal(templates[2].Id, recommendation!.TemplateId);
            Assert.Equal(templates[3].Id, recommendation.HigherOption!.TemplateId);
            var request = new ConfirmNextStudentProgramRequest(templates[3].Id, source.Id, post.Id, true);
            if (managed)
            {
                await programs.RequestNextProgramAsync(user, request, CancellationToken.None);
                await programs.RequestNextProgramAsync(user, request, CancellationToken.None);
                Assert.False(await db.StudentProgramProgresses.AnyAsync(item => item.IsActive));
                await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => programs.ConfirmNextProgramAsync(user, request, CancellationToken.None));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => programs.ApproveNextProgramAsync(Guid.NewGuid(), user, request, null, CancellationToken.None));
            }
            var created = managed ? await programs.ApproveNextProgramAsync(teacher, user, request, null, CancellationToken.None)
                : await programs.ConfirmNextProgramAsync(user, request, CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.StudentProgramProgresses.CountAsync(item => item.IsActive));
            var decision = await db.ProgramContinuations.SingleAsync();
            Assert.Equal(created.ProgramId, decision.NewProgressId);
            Assert.Equal(3, decision.RecommendedLevel);
            Assert.Equal(templates[3].Id, decision.TemplateId);
            var history = await programs.GetMyProgramsAsync(user, CancellationToken.None);
            Assert.Equal(4, history.Single(item => item.ProgressId == created.ProgramId).SelectedTrainingLevel);
            var pending = await assessment.GetFollowUpPlansAsync(user, CancellationToken.None);
            Assert.True(Assert.Single(pending).AdditionalTraining);
            Assert.Equal(AssessmentAttemptPhase.Retention, pending[0].Plan.NextPhase);
            var followup = await assessment.StartAttemptAsync(user, new StartAssessmentAttemptRequest
                { Phase = AssessmentAttemptPhase.Retention, ProgramProgressId = source.Id }, CancellationToken.None);
            Assert.Equal(source.Id, followup.ProgramProgressId);
            Assert.Equal(3, (await assessment.GetAttemptExercisesAsync(user, followup.Id, CancellationToken.None)).Exercises.Count);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => assessment.GetProgramPhasePlanAsync(Guid.NewGuid(), source.Id, CancellationToken.None));
            Assert.True((await db.StudentProgramProgresses.SingleAsync(item => item.Id == created.ProgramId)).IsActive);
            Assert.Equal(6, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    public class AccessProxy : DispatchProxy
    {
        public Guid Teacher { get; set; }
        public Guid Student { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult((Guid)args![0]! == Teacher && (Guid)args[1]! == Student);
    }
}

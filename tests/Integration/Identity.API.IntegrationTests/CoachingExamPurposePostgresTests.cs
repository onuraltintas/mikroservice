using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.API.IntegrationTests;

// KM1 (01.11.2026): AGS (+ÖABT), YDS and YÖKDİL preparation end to end through the setup service and the primary goal API.
public sealed class CoachingExamPurposePostgresTests
{
    [CoachingProfilePostgresFact]
    public async Task NewPurposesChooseTheirLessonsSessionAndNetScopes()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")) { Pooling = false };
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        var database = "test_coaching_purposes_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
            await using var db = new CoachingDbContext(options);
            await db.Database.MigrateAsync();
            var student = Guid.NewGuid(); var profile = CoachingStudentProfile.Create(student);
            profile.UpdateEducation(CoachingEducationStatus.Graduate, null, null, null, null, null, null, null);
            var curriculum = CurriculumVersion.Create("dil-2026", "Dil sınavları", "v1", 2026, 2027, "Test", null); curriculum.SetPublished(true);
            var yds = StudyCatalogExam.Create("YDS", "Yabancı Dil Bilgisi Seviye Tespit Sınavı");
            yds.ReplaceSessions([new(null, new(2027, 3, 28), "2027/1", new(2027, 2, 3), new(2027, 2, 11)), new(null, new(2027, 11, 21), "2027/2", null, null),
                new(null, new(2026, 3, 29), "2026/1", null, null)]);
            var yokdil = StudyCatalogExam.Create("YOKDIL", "Yükseköğretim Kurumları Yabancı Dil Sınavı"); yokdil.SetActive(false);
            yokdil.ReplaceSessions([new(null, new(2027, 2, 28), "2027/1", null, null)]);
            var ags = StudyCatalogExam.Create("AGS", "Akademi Giriş Sınavı"); var oabt = StudyCatalogExam.Create("OABT", "Öğretmenlik Alan Bilgisi Testi");
            ags.ReplaceSessions([new(null, new(2027, 5, 8), "2027", null, null)]);
            db.AddRange(profile, curriculum, yds, yokdil, ags, oabt); await db.SaveChangesAsync();
            StudyCatalogLesson Lesson(string id, string name, string exam)
            { var lesson = StudyCatalogLesson.Create("test", id, name, null, exam); lesson.AssignCurriculum(curriculum.Id); lesson.SetActive(true); return lesson; }
            var english = Lesson("yds-en", "YDS İngilizce", "YDS"); var general = Lesson("ags-gy", "Sözel Yetenek", "AGS");
            var field = Lesson("oabt-ozel", "Özel Eğitim Alan Bilgisi", "OABT"); var lgs = Lesson("lgs-mat", "Matematik", "LGS");
            db.AddRange(english, general, field, lgs); await db.SaveChangesAsync();
            foreach (var lesson in new[] { english, general, field, lgs })
            {
                var unit = StudyCatalogUnit.Create("test", lesson.SourceId + "-u", lesson.Id, "Ünite", 1); unit.SetActive(true);
                var topic = StudyCatalogTopic.Create("test", lesson.SourceId + "-t", lesson.Id, unit.Id, "Konu", null, 1, 30); topic.SetActive(true);
                db.AddRange(unit, topic);
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            var actor = new CoachingOwnedProfilePersistenceTests.Actor { UserId = student, Roles = ["Student"] };
            var accounts = new CoachingOwnedProfilePersistenceTests.FakeAccounts();
            accounts.Users[student] = new(student, "Test", "Student", "test@example.test", true, true, ["Student"], null);
            var service = new CoachingLearningSetupService(db, new CoachingAccessPolicy(actor), accounts);
            var autumn = yds.Sessions.Single(x => x.Label == "2027/2").Id;

            var saved = await service.SaveAsync(new(null, 2026, LearningPurpose.Yds, 2027, null, null, [], [], false, autumn), default);
            Assert.Equal((LearningPurpose.Yds, "English", (Guid?)autumn), (saved.Context!.Purpose, saved.Context.ForeignLanguage, saved.Context.TargetExamSessionId));
            // Only upcoming sessions of active exams are offered; the inactive YÖKDİL draft and the past YDS session are not.
            Assert.Equal(new[] { "AGS:2027", "YDS:2027/1", "YDS:2027/2" }, saved.ExamSessions.Select(x => x.ExamCode + ":" + x.Label).ToArray());
            Assert.Equal(new DateOnly(2027, 2, 11), saved.ExamSessions.Single(x => x.Label == "2027/1").ApplicationEnd);
            var catalog = await service.GetCatalogAsync(null, 1, default);
            Assert.Equal(new[] { "YDS İngilizce" }, catalog.Lessons.Select(x => x.Name).ToArray());
            db.ChangeTracker.Clear();

            var version = saved.Context.Version;
            var sessionOfAnotherExam = ags.Sessions.Single().Id;
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(version, 2026, LearningPurpose.Yds, 2027, null, null, [], [], false, sessionOfAnotherExam), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(version, 2026, LearningPurpose.Yds, 2028, null, null, [], [], false, autumn), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(version, 2026, LearningPurpose.Yds, 2026, null, null, [], [],
                false, yds.Sessions.Single(x => x.Label == "2026/1").Id), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new(version, 2026, LearningPurpose.Yokdil, 2027, "YOKDIL_FEN", null, [], [],
                false, yokdil.Sessions.Single().Id), default));
            db.ChangeTracker.Clear();

            var agsSaved = await service.SaveAsync(new(version, 2026, LearningPurpose.Ags, 2027, "OABT_OZEL", null, [], [], false), default);
            Assert.Null(agsSaved.Context!.TargetExamSessionId);
            Assert.Equal(new[] { "Sözel Yetenek", "Özel Eğitim Alan Bilgisi" }, (await service.GetCatalogAsync(null, 1, default)).Lessons.Select(x => x.Name).Order(StringComparer.Ordinal).ToArray());
            db.ChangeTracker.Clear();

            // The primary goal accepts net targets of the purpose's scopes only.
            var goal = AcademicGoal.Create(student, "AGS hedefim", GoalCategory.ExamPreparation); goal.SetNetTarget("AGS", 60); goal.SetNetTarget("OABT_OZEL", 35);
            var wrong = AcademicGoal.Create(student, "Yanlış kapsam", GoalCategory.ExamPreparation); wrong.SetNetTarget("OABT_TUR", 30);
            db.AddRange(goal, wrong); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var api = new StudentPrimaryGoalController(db, new CoachingAccessPolicy(actor));
            Assert.IsType<BadRequestObjectResult>(await api.Save(new(wrong.Id, null), default));
            Assert.IsType<OkObjectResult>(await api.Save(new(goal.Id, null), default));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}

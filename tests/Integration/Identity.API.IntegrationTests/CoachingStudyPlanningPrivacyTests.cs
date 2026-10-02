using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyPlanningPrivacyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Privacy_ExportsPlanningAndErasesOnlyTheSubjectsOwnedAggregates()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid(); var other = Guid.NewGuid();
            var exam = Exam.CreateStudentReported(student, "My test", ExamType.Mock, DateTime.UtcNow, 100);
            var result = ExamResult.Create(exam.Id, student, 80);
            result.SetAnswerStatistics(8, 1, 1);
            result.SetLessonAnswers([new(Guid.NewGuid(), null, 10, 8, 1, 1, "Math")]);
            exam.AddResult(result);
            var teacherExam = Exam.Create(Guid.NewGuid(), "Teacher test", ExamType.Mock, DateTime.UtcNow, 100);
            teacherExam.AddResult(ExamResult.Create(teacherExam.Id, student, 60));
            var otherExam = Exam.CreateStudentReported(other, "Other test", ExamType.Mock, DateTime.UtcNow, 100);
            otherExam.AddResult(ExamResult.Create(otherExam.Id, other, 70));
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan");
            plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026, 10, 1), "Review", 30); task.Complete(25);
            var hours = StudyAvailability.Create(student, "Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Monday, 600, 660)]);
            var otherHours = StudyAvailability.Create(other, "Europe/Istanbul");
            db.AddRange(exam, teacherExam, otherExam, plan, task, hours, otherHours); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal("StudentReported", export.Exams.Single(x => x.ExamId == exam.Id).Source);
            Assert.Single(export.Exams.Single(x => x.ExamId == exam.Id).LessonAnswers!);
            Assert.Equal(25, export.StudyPlans!.Single().Tasks.Single().ActualMinutes);
            Assert.Equal("Europe/Istanbul", export.StudyAvailability!.TimeZoneId);
            var requestId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var now = DateTime.UtcNow;
            var assessment = await new CoachingErasureAssessmentService(db, TimeProvider.System)
                .AssessAsync(new(eventId, requestId, student, now, true, PersonalDataScope.Coaching), CancellationToken.None);
            Assert.Equal(4, assessment.StudyPlanningRecordCount);
            var erasure = new CoachingErasureExecutionService(db, new NoAttachments(), TimeProvider.System);
            var message = new PersonalDataErasureExecutionRequestedV1(eventId, requestId, student, now, PersonalDataScope.Coaching);
            var executed = await erasure.ExecuteAsync(message, CancellationToken.None);
            Assert.Equal(6, executed.DeletedRecordCount);
            db.ChangeTracker.Clear();
            Assert.False(await db.Exams.AnyAsync(x => x.StudentOwnerId == student));
            Assert.False(await db.ExamResults.AnyAsync(x => x.StudentId == student));
            Assert.False(await db.StudyPlanRevisions.AnyAsync(x => x.StudentId == student));
            Assert.False(await db.StudyPlanTasks.AnyAsync(x => x.StudentId == student));
            Assert.False(await db.StudyAvailability.AnyAsync(x => x.StudentId == student));
            Assert.True(await db.Exams.AnyAsync(x => x.Id == teacherExam.Id));
            Assert.True(await db.Exams.AnyAsync(x => x.StudentOwnerId == other));
            Assert.True(await db.StudyAvailability.AnyAsync(x => x.StudentId == other));
            Assert.Equal(executed.Id, (await erasure.ExecuteAsync(message, CancellationToken.None)).Id);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class NoAttachments : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string storageKey, Stream content, string expectedContentType, long expectedSizeBytes, string expectedSha256, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

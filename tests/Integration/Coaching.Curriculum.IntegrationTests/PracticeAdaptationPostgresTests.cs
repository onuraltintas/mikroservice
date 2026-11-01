using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeAdaptationPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AllSixProfilesUseTheirDynamicCatalogAndCanPreviewAndApplyReviews()
    {
        await using var f = await Fixture.Create();
        // The six LGS/YKS scopes have official lesson aliases in the catalog; KM1's AGS/ÖABT/YDS/YÖKDİL catalogs come later.
        foreach (var profile in PracticeExamRules.Profiles.Where(x => x.Code is "LGS" or "TYT" or "AYT_SAY" or "AYT_EA" or "AYT_SOZ" or "YDT_EN"))
        {
            var student = Guid.NewGuid(); f.Pg.Data.User.UserId = student;
            var section = profile.Sections[0]; var scope = profile.Code.StartsWith("AYT_") ? "AYT" : profile.Code == "YDT_EN" ? "YDT" : profile.Code;
            var lessonName = section.Code switch { "TUR" => "Türkçe", "MAT" => "Matematik", "EDB" => "Türk Dili ve Edebiyatı", _ => "İngilizce" };
            if (scope != "LGS") lessonName += " (" + scope + ")";
            var lesson = StudyCatalogLesson.Create("adaptation-test", profile.Code, lessonName, null, scope); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("adaptation-test", profile.Code, lesson.Id, "Ünite", 0); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("adaptation-test", profile.Code, lesson.Id, unit.Id, "Konu", null, 0, 60); topic.SetActive(true);
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, profile.Name); plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026,10,19), "Önceki çalışma",30,topic.Id); task.Complete(30);
            var hours = StudyAvailability.Create(student,"Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Wednesday,1080,1200),new(DayOfWeek.Thursday,1080,1200)]);
            f.Db.AddRange(lesson,unit,topic,plan,task,hours);
            for (var i=1;i<=3;i++)
            {
                var input = PracticeExamTests.Blank(profile);
                input = input with { Sections = input.Sections.Select(x => x.SectionCode == section.Code ? x with { Wrong=4, Empty=section.QuestionCount-4 } : x).ToArray() };
                var totals = PracticeExamRules.Summarize(input);
                var exam = Exam.CreateStudentReported(student,"Deneme "+i,scope=="LGS"?ExamType.LGS:ExamType.YKS,new DateTime(2026,10,i,0,0,0,DateTimeKind.Utc),500);
                var result = ExamResult.Create(exam.Id,student,250); result.SetAnswerStatistics(totals.Correct,totals.Wrong,totals.Empty); result.SetPracticeExam(input,exam.ExamType);
                result.SetTopicAnalysis(new([new(section.Code,topic.Id,0,4,1,"Knowledge")],profile.Code+"-"+i)); exam.AddResult(result);f.Db.Add(exam);
            }
            await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
            Assert.Contains(await f.Service.TopicsAsync(profile.Code,section.Code,default),x=>x.Id==topic.Id);
            var advice = Assert.Single((await f.Service.GetAsync(null,default)).Advice); Assert.Single(advice.Topics);
            var request = new PracticeAdjustmentRequest(profile.Code,"StudentReported",new(2026,11,3),false,[new(topic.Id,60)]);
            var preview=await f.Service.PreviewAsync(null,request,default);Assert.True(preview.CanApply);
            var saved=await f.Plans.ApplyPracticeAsync(new(preview.Fingerprint,request,true),f.Service,null,default);
            Assert.Equal(3,saved.Tasks.Count);Assert.True(saved.Tasks.Single(x=>x.Id==task.Id).IsCompleted);
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ImportedScopeSuffixesMatchOnlyTheirOfficialSubjectsAndScopes()
    {
        await using var f = await Fixture.Create();
        var cases = new[] {
            ("TYT", "MAT", "TYT", "Matematik (TYT)", true),
            ("TYT", "FEL", "TYT", "Felsefe (TYT/AYT)", true),
            ("AYT_SAY", "MAT", "AYT", "Matematik (AYT)", true),
            ("AYT_SAY", "FIZ", "AYT", "Fizik (AYT)", true),
            ("AYT_EA", "EDB", "AYT", "Türk Dili ve Edebiyatı (AYT)", true),
            ("AYT_SOZ", "TAR2", "AYT", "Tarih (AYT)", true),
            ("AYT_SOZ", "FEL", "AYT", "Psikoloji (AYT)", true),
            ("YDT_EN", "ENG", "YDT", "İngilizce (YDT)", true),
            ("TYT", "MAT", "TYT", "Matematik (AYT)", false),
            ("AYT_SAY", "MAT", "TYT", "Matematik (TYT)", false),
            ("TYT", "MAT", "TYT", "Özel Matematik (TYT)", false),
            ("YDT_EN", "ENG", "YDT", "Arapça (YDT)", false)
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var (exam, section, scope, name, expected) = cases[i];
            var lesson = StudyCatalogLesson.Create("suffix-regression", i.ToString(), name, null, scope); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("suffix-regression", i.ToString(), lesson.Id, "Ünite", 0); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("suffix-regression", i.ToString(), lesson.Id, unit.Id, "Konu", null, 0, 30); topic.SetActive(true);
            f.Db.AddRange(lesson, unit, topic); await f.Db.SaveChangesAsync();
            Assert.Equal(expected, (await f.Service.TopicsAsync(exam, section, default)).Any(x => x.Id == topic.Id));
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentAnalysisPreviewAndConfirmedApplyPreserveAllOldTasksAndLinksAndAreIdempotent()
    {
        await using var f = await Fixture.Create();
        var before = await f.Snapshot();
        var report = await f.Service.GetAsync(null, default);
        var selected = report.Exams[0];
        Assert.Equal(3, report.Advice.Single().ExamIds.Count);
        Assert.Single(report.Advice.Single().Topics);
        var request = f.Request();
        var preview = await f.Service.PreviewAsync(null, request, default);
        Assert.True(preview.CanApply); Assert.Equal(2, preview.Slots.Count);
        Assert.NotEqual(preview.Slots[0].Date, preview.Slots[1].Date);
        Assert.All(preview.Slots, x => Assert.Equal(30, x.Minutes));
        Assert.Equal(before, await f.Snapshot()); // Preview is read-only.
        await Assert.ThrowsAsync<ArgumentException>(() => f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, request, false), f.Service, null, default));
        var saved = await f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, request, true), f.Service, null, default);
        Assert.Equal(before, await f.Snapshot());
        Assert.Equal(3, saved.Tasks.Count); Assert.Equal(2, saved.Tasks.Count(x => x.SourceName?.StartsWith("DenemeTekrar:") == true));
        var replay = await f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, request, true), f.Service, null, default);
        Assert.Equal(saved.Tasks.Count, replay.Tasks.Count);
        Assert.True((await f.Service.GetAsync(null, default)).Advice.Single().Topics.Single().AlreadyPlanned);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(null, request, default));
        // Old analyses cannot overwrite a newer analysis, and results cannot claim another subject.
        var valid = new PracticeAnalysisSave(selected.Fingerprint, selected.Analysis with { TestIdentity = "renamed-test" });
        var updated = await f.Service.SaveAnalysisAsync(null, selected.ExamId, valid, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAnalysisAsync(null, selected.ExamId, valid, default));
        await f.Service.SaveAnalysisAsync(null, selected.ExamId, new(updated.Fingerprint, updated.Analysis), default);
        var fresh = (await f.Service.GetAsync(null, default)).Exams.Single(x => x.ExamId == selected.ExamId);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAnalysisAsync(null, selected.ExamId,
            new(fresh.Fingerprint, new([new("TUR", f.Topic.Id, 0, 1, 0, "Knowledge")])), default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StaleEvidenceAndCalendarChangesRejectApprovalWithoutPartialWrites()
    {
        await using var f = await Fixture.Create(); var request = f.Request();
        var preview = await f.Service.PreviewAsync(null, request, default);
        var exam = (await f.Service.GetAsync(null, default)).Exams[0];
        await f.Service.SaveAnalysisAsync(null, exam.ExamId, new(exam.Fingerprint, exam.Analysis with { TestIdentity = "changed" }), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, request, true), f.Service, null, default));
        Assert.Single(await f.Db.StudyPlanTasks.ToArrayAsync());
        preview = await f.Service.PreviewAsync(null, request, default);
        var occupied = StudyPlanTask.Create(f.Plan, preview.Slots[0].Date, "Yeni sabit", 30, isPinned: true);
        occupied.Schedule(occupied.PlannedDate, preview.Slots[0].StartMinute); f.Db.Add(occupied); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, request, true), f.Service, null, default));
        Assert.Equal(2, await f.Db.StudyPlanTasks.CountAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task InsufficientSpaceAndDeadlineExtensionAreExplicitAndDoNotModifyCompletedWork()
    {
        await using var f = await Fixture.Create(deadline: new(2026, 10, 21));
        var request = f.Request() with { EndDate = new(2026, 10, 21) };
        var partial = await f.Service.PreviewAsync(null, request, default);
        Assert.False(partial.CanApply); Assert.Single(partial.Slots); Assert.Equal(30, partial.Unscheduled.Single().Minutes);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(partial.Fingerprint, request, true), f.Service, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(null, f.Request(), default));
        var extended = f.Request() with { AllowDeadlineExtension = true };
        var preview = await f.Service.PreviewAsync(null, extended, default); Assert.True(preview.CanApply);
        var result = await f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, extended, true), f.Service, null, default);
        Assert.Equal(extended.EndDate, result.WeeklySource!.EndDate);
        Assert.True(result.Tasks.Single(x => x.Title == "Çarpanlar").IsCompleted);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherCanAnalyseAndApproveAssignedStudentButStudentCannotApplyTeacherPlan()
    {
        await using var f = await Fixture.Create(teacherPlan: true);
        var own = await f.Service.PreviewAsync(null, f.Request(), default);
        Assert.False(own.CanApply); Assert.True(own.TeacherApprovalRequired);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(own.Fingerprint, f.Request(), true), f.Service, null, default));
        f.Pg.Data.User.UserId = f.Teacher; f.Pg.Data.User.Roles = ["Teacher"];
        var teacher = await f.Service.GetAsync(f.Student, default); Assert.All(teacher.Exams, x => Assert.True(x.CanEdit));
        var preview = await f.Service.PreviewAsync(f.Student, f.Request(), default); Assert.True(preview.CanApply);
        var saved = await f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, f.Request(), true), f.Service, f.Student, default);
        Assert.Equal(3, saved.Tasks.Count); Assert.Single(f.Events.Messages);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.GetAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, f.Request(), true), f.Service, Guid.NewGuid(), default));
        f.Pg.Data.User.UserId = f.Student; f.Pg.Data.User.Roles = ["Student"];
        Assert.Equal(12, (await f.Service.GetAsync(null, default)).Advice.Single().Topics.Single().Evidence.Wrong); // original analysis retained
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Plans.ApplyPracticeAsync(new(preview.Fingerprint, f.Request(), true), f.Service, null, default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AnswerCorrectionsInvalidateTopicAnalysisAndForeignOrInactiveTopicsAreRejected()
    {
        await using var f = await Fixture.Create(); var report = await f.Service.GetAsync(null, default);
        var exam = report.Exams[0]; var changed = exam.Input with { Sections = exam.Input.Sections.Select(x => x.SectionCode == "MAT" ? x with { Correct = 1, Wrong = 3, Empty = 16 } : x).ToArray() };
        var tracked = await f.Db.Exams.Include(x => x.Results).SingleAsync(x => x.Id == exam.ExamId);
        tracked.UpdateResult(tracked.Results.Single().Id, 260, 0, 4, 86, null, null, null, exam.Input);
        await f.Db.SaveChangesAsync();
        Assert.NotEmpty((await f.Service.GetAsync(null, default)).Exams.Single(x => x.ExamId == exam.ExamId).Analysis.Topics);
        tracked.UpdateResult(tracked.Results.Single().Id, 250, 1, 3, 86, null, null, null, changed);
        await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Service.GetAsync(null, default)).Exams.Single(x => x.ExamId == exam.ExamId).Analysis.Topics);
        var fresh = (await f.Service.GetAsync(null, default)).Exams[0];
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAnalysisAsync(null, fresh.ExamId,
            new(fresh.Fingerprint, new([new("MAT", Guid.NewGuid(), 0, 1, 0, null)])), default));
        (await f.Db.StudyCatalogTopics.SingleAsync(x => x.Id == f.Topic.Id)).SetActive(false); await f.Db.SaveChangesAsync();
        Assert.Empty(await f.Service.TopicsAsync("LGS", "MAT", default));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CurriculumPostgresTests.Pg Pg = null!;
        public Coaching.Infrastructure.Data.CoachingDbContext Db => Pg.Db;
        public StudyCatalogTopic Topic = null!; public StudyPlanRevision Plan = null!;
        public Guid Student; public Guid Teacher = Guid.NewGuid(); public Publisher Events = new();
        public CoachingPracticeAdaptationService Service = null!; public CoachingManualStudyPlanService Plans = null!;
        public static async Task<Fixture> Create(DateOnly? deadline = null, bool teacherPlan = false)
        {
            var f = new Fixture { Pg = await CurriculumPostgresTests.Pg.Create() };
            f.Topic = await f.Pg.Data.Seed(); var original = await f.Db.StudyPlanRevisions.SingleAsync(); f.Student = original.StudentId;
            if (teacherPlan)
            {
                // Preserve the completed seed revision and use a new teacher-owned active one.
                original.Archive(); f.Plan = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Öğretmen planı", f.Teacher);
                f.Plan.Activate(); f.Db.Add(f.Plan);
                var completed = StudyPlanTask.Create(f.Plan, new(2026,10,19), "Çarpanlar", 30, f.Topic.Id); completed.Complete(25); f.Db.Add(completed);
            }
            else f.Plan = original;
            if (deadline.HasValue)
            {
                // Source metadata can only be set while a plan is a draft.
                await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.study_plan_revisions SET \"WeeklySource\"={JsonSerializer.Serialize(new WeeklyPlanSource(0,null,new(2026,10,1),deadline.Value,false,"test",null,null))}::jsonb WHERE \"Id\"={f.Plan.Id}");
                await f.Db.Entry(f.Plan).ReloadAsync();
            }
            var hours = StudyAvailability.Create(f.Student, "Europe/Istanbul");
            hours.ReplaceWindows([new(DayOfWeek.Wednesday, 1080, 1140), new(DayOfWeek.Thursday, 1080, 1140)]);
            f.Db.Add(hours); f.Pg.Data.User.UserId = f.Student; f.Pg.Data.User.Roles = ["Student"];
            for (var i=1; i<=3; i++)
            {
                var input = PracticeExamTests.Blank(PracticeExamRules.Profiles.Single(x => x.Code == "LGS"));
                input = input with { Sections = input.Sections.Select(x => x.SectionCode == "MAT" ? new PracticeSectionAnswers("MAT", 0, 4, 16) : x).ToArray() };
                var exam = Exam.CreateStudentReported(f.Student, "Deneme " + i, ExamType.LGS, new DateTime(2026,10,i,0,0,0,DateTimeKind.Utc),500);
                var result = ExamResult.Create(exam.Id, f.Student, 250); result.SetAnswerStatistics(0,4,86); result.SetPracticeExam(input,ExamType.LGS);
                result.SetTopicAnalysis(new([new("MAT",f.Topic.Id,0,4,1,"Knowledge")],"deneme-"+i)); exam.AddResult(result); f.Db.Add(exam);
            }
            await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            var access = new CoachingAccessPolicy(f.Pg.Data.User); var staff = new CoachingStudyPlanAccess(access, new Identity(f.Student), f.Pg.Data.Scope);
            f.Service = new(f.Db,access,staff,new Clock());
            f.Plans = new(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),f.Events,staff);
            return f;
        }
        public PracticeAdjustmentRequest Request() => new("LGS","StudentReported",new(2026,11,3),false,[new(Topic.Id,60)]);
        public async Task<string> Snapshot() => JsonSerializer.Serialize(new {
            Tasks = await Db.StudyPlanTasks.AsNoTracking().Where(x => x.SourceName == null).OrderBy(x => x.Id).ToArrayAsync(),
            Links = await Db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x => x.TaskId).ToArrayAsync() });
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026,10,20,18,0,0,TimeSpan.Zero); }
    private sealed class Publisher : ICoachingEventPublisher
    { public List<object> Messages = []; public Task PublishAsync<T>(T message, CancellationToken ct=default) where T:class { Messages.Add(message); return Task.CompletedTask; } }
    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id,CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher,IReadOnlyCollection<Guid> ids,Guid? institution,bool admin,CancellationToken ct)
            => ids.All(x => x == student) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden","Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer,IReadOnlyCollection<Guid> ids,CancellationToken ct) => Task.FromResult(ids);
    }
}

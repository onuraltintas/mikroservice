using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Curriculum.IntegrationTests;

// Student panel T6 (31.10.2026): one list of a student's exams — their own, their teacher's and their institution's — with net
// trends and subject averages; future exams and a self-reported duplicate of a recorded deneme are left out.
public sealed class StudentExamOverviewPostgresTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task OneListWithSourcesRanksTrendsAndSubjects()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var other = Guid.NewGuid(); var third = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var institution = Guid.NewGuid(); var admin = Guid.NewGuid();

        var own = Own(student, "Kendi TYT denemem", Now.AddDays(-20), Tyt(20, 15, 1, 4));
        var teacherExam = Exam.Create(teacher, "Öğretmen TYT denemesi", ExamType.YKS, Now.AddDays(-10), 500);
        teacherExam.AddResult(Result(teacherExam, student, Tyt(25, 16, 1, 5)));
        var wide = Exam.CreateInstitutionWide(admin, institution, "Kurum TYT 1", "TYT", Now.AddDays(-3), 500, 12);
        wide.ImportResult(student, 300, Tyt(30, 18, 2, 5));
        wide.ImportResult(other, 320, Tyt(35, 20, 4, 6));
        wide.ImportResult(third, 280, Tyt(10, 5, 1, 1));
        // The student also typed the institution's deneme in on the same Türkiye day: a duplicate, left out.
        var copy = Own(student, "Kurum denemesi (benim girişim)", Now.AddDays(-3).AddHours(2), Tyt(29, 18, 2, 5));
        var future = Own(student, "Yarınki deneme", Now.AddDays(1), Tyt(40, 40, 5, 7));
        var lgsLike = Exam.CreateStudentReported(student, "Kısa sınav", ExamType.Quiz, Now.AddDays(-1), 100);
        var quiz = ExamResult.Create(lgsLike.Id, student, 80); quiz.SetAnswerStatistics(8, 2, 0); lgsLike.AddResult(quiz);
        pg.Db.AddRange(own, teacherExam, wide, copy, future, lgsLike);
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var overview = await new CoachingStudentExamOverviewService(pg.Db, new Reader(student), new Clock(Now)).GetAsync(student, student, default);

        Assert.Equal(["Kısa sınav", "Kurum TYT 1", "Öğretmen TYT denemesi", "Kendi TYT denemem"], overview.Exams.Select(x => x.Title));
        Assert.Equal(["Student", "Institution", "Teacher", "Student"], overview.Exams.Select(x => x.Source));
        var institutionRow = overview.Exams.Single(x => x.Source == "Institution");
        Assert.Equal((2, 3), (institutionRow.Rank!.Value, institutionRow.Participants!.Value));
        Assert.Equal("TYT", institutionRow.ExamCode);
        Assert.Null(overview.Exams.Single(x => x.Title == "Kısa sınav").Net);

        var trend = Assert.Single(overview.Trends);
        Assert.Equal("TYT", trend.ExamCode);
        Assert.Equal(120, trend.MaxNet);
        Assert.Equal(["Student", "Teacher", "Institution"], trend.Points.Select(x => x.Source));
        Assert.True(trend.Points.Zip(trend.Points.Skip(1)).All(x => x.First.ExamDate < x.Second.ExamDate));

        var subjects = overview.Subjects.Where(x => x.ExamCode == "TYT").ToArray();
        Assert.Equal(9, subjects.Length);
        Assert.All(subjects, x => Assert.Equal(3, x.ExamsUsed));
        Assert.Equal("TAR", Assert.Single(subjects, x => x.Weakest).SectionCode); // 1.33 of 5 is the lowest share
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task OnlyViewersAllowedToReadTheStudentGetTheList()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var parent = Guid.NewGuid(); var otherParent = Guid.NewGuid();
        pg.Db.Add(Own(student, "Kendi denemem", Now.AddDays(-2), Tyt(20, 15, 1, 4)));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        var service = new CoachingStudentExamOverviewService(pg.Db, new Reader(student, parent), new Clock(Now));

        // A parent whose link is not approved (or another student's parent) is refused.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(otherParent, student, default));
        Assert.Equal("Authorization.Forbidden", error.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(Guid.Empty, student, default));
        Assert.Equal("Kendi denemem", Assert.Single((await service.GetAsync(student, student, default)).Exams).Title);
        Assert.Equal("Kendi denemem", Assert.Single((await service.GetAsync(parent, student, default)).Exams).Title);
        // Someone else's list stays empty for a student with no exams.
        var empty = await service.GetAsync(otherParent, otherParent, default);
        Assert.Empty(empty.Exams); Assert.Empty(empty.Trends); Assert.Empty(empty.Subjects);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task BadgesShowHowFarTheStudentIs()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid();
        pg.Db.AddRange(Own(student, "Deneme 1", Now.AddDays(-9), Tyt(20, 15, 1, 4)), Own(student, "Deneme 2", Now.AddDays(-5), Tyt(22, 15, 1, 4)),
            Own(student, "Deneme 3", Now.AddDays(-2), Tyt(24, 15, 1, 4)), Own(student, "Gelecek deneme", Now.AddDays(3), Tyt(24, 15, 1, 4)));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var motivation = await new CoachingMotivationService(pg.Db, new Reader(student), new Clock(Now)).GetAsync(student, student, default);

        var exams = motivation.Badges.Single(x => x.Code == "five-exams");
        Assert.Equal(("5 deneme", 3, 5, (DateOnly?)null), (exams.Label, exams.Progress, exams.Target, exams.EarnedOn));
        Assert.Equal((0, 7), motivation.Badges.Where(x => x.Code == "streak-7").Select(x => (x.Progress, x.Target)).Single());
        Assert.Equal((0, 10), motivation.Badges.Where(x => x.Code == "ten-submissions").Select(x => (x.Progress, x.Target)).Single());
    }

    // TYT: Türkçe 40, Temel Matematik 40, Tarih 5 and six more sections; the rest are answered at a steady 3-4; no wrong answers.
    private static PracticeExamInput Tyt(int tur, int mat, int tar, int fiz) => new("TYT",
        PracticeExamRules.Profiles.Single(x => x.Code == "TYT").Sections.Select(section =>
        {
            var correct = section.Code switch { "TUR" => tur, "MAT" => mat, "TAR" => tar, "FIZ" => fiz, "DIN" => 4, _ => 3 };
            return new PracticeSectionAnswers(section.Code, correct, 0, section.QuestionCount - correct);
        }).ToArray());

    private static Exam Own(Guid student, string title, DateTime date, PracticeExamInput practice)
    {
        var exam = Exam.CreateStudentReported(student, title, ExamType.YKS, date, 500);
        exam.AddResult(Result(exam, student, practice));
        return exam;
    }

    private static ExamResult Result(Exam exam, Guid student, PracticeExamInput practice)
    {
        var totals = PracticeExamRules.Summarize(practice);
        var result = ExamResult.Create(exam.Id, student, 250);
        result.SetAnswerStatistics(totals.Correct, totals.Wrong, totals.Empty);
        result.SetPracticeExam(practice, ExamType.YKS);
        return result;
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    // Stands in for Identity: only the given viewers (an approved parent, a teacher of the student) may read the student.
    private sealed class Reader(Guid student, params Guid[] allowed) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) => Task.FromResult(institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(allowed.Contains(viewerUserId) && studentIds.Contains(student) ? studentIds : []);
    }
}

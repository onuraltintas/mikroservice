using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O4: an institution runs one practice exam for all its students, imports results from a CSV file and
// sees ranking, percentile and branch (şube) analysis.
public sealed class CoachingInstitutionExamTests
{
    private static readonly DateOnly Day = new(2026, 11, 8);

    [Fact]
    public async Task AnInstitutionAdminCreatesAnExamForAGradeAndListsIt()
    {
        await using var s = await Setup.CreateAsync();

        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("Kasım TYT denemesi", "TYT", Day, 12, 500), default);

        exam.Should().Match<InstitutionExamItem>(x => x.ExamCode == "TYT" && x.GradeLevel == 12 && x.Participants == 0 && x.ExamName == "TYT");
        var stored = await s.F.Db.Exams.SingleAsync();
        (stored.InstitutionId, stored.CreatedByAdminId, stored.CreatedByTeacherId, stored.ExamType, stored.PracticeExamCode, stored.TargetGradeLevel)
            .Should().Be((s.Institution, s.Admin, (Guid?)null, ExamType.YKS, "TYT", 12));
        (await s.Exams.ListAsync(s.Admin, s.Institution, 1, 20, default)).Items.Should().ContainSingle(x => x.Id == exam.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Exams.CreateAsync(s.Admin, s.Institution, new("Bilinmeyen", "XYZ", Day, null, 500), default));
    }

    [Fact]
    public async Task OnlyTheInstitutionsOwnAdminCanManageItsExams()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS", "LGS", Day, null, 500), default);
        var outsider = s.F.AddAccount("InstitutionAdmin"); var other = Guid.NewGuid(); s.F.Accounts.Institutions.Add(other); s.F.Accounts.Admins[outsider] = new(false, other);

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Exams.GetAsync(outsider, s.Institution, exam.Id, null, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Exams.GetAsync(outsider, other, exam.Id, null, default));
    }

    [Fact]
    public async Task ACsvFileIsPreviewedThenAppliedAndProducesRankingPercentileAndBranches()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS denemesi", "LGS", Day, 8, 500), default);
        var csv = s.LgsCsv(
            (s.Ada, 15, 3, 400m), (s.Bora, 18, 0, 470m), (s.Cem, 15, 3, 395m));

        var preview = await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, csv, default);
        preview.Errors.Should().BeEmpty();
        preview.Accepted.Should().HaveCount(3);
        (await s.F.Db.ExamResults.CountAsync()).Should().Be(0, "a preview changes nothing");

        (await s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, csv, preview.Fingerprint, default)).Should().Be(3);
        var report = await s.Exams.GetAsync(s.Admin, s.Institution, exam.Id, null, default);

        report.Exam.Participants.Should().Be(3);
        var bora = report.Results[0];
        (bora.StudentId, bora.Rank, bora.TopPercent, bora.Score, bora.ClassBranch).Should().Be((s.Bora, 1, 33.3m, 470m, "8-B"));
        bora.Net.Should().Be(84m, "18 correct in each 20-question section and all 10-question sections right");
        report.Results.Where(x => x.StudentId != s.Bora).Should().OnlyContain(x => x.Rank == 2 && x.TopPercent == 66.7m && x.Net == 72m, "equal nets share a rank");
        report.Branches.Should().BeEquivalentTo(new[]
        {
            new InstitutionExamGroup("8-A", 2, report.Results.Single(x => x.StudentId == s.Ada).Net, report.Results.Single(x => x.StudentId == s.Ada).Net),
            new InstitutionExamGroup("8-B", 1, bora.Net, bora.Net)
        });
        report.Sections.Should().HaveCount(6).And.Contain(x => x.Code == "TUR");
        (await s.Exams.GetAsync(s.Admin, s.Institution, exam.Id, "8-A", default)).Results.Should().HaveCount(2).And.OnlyContain(x => x.ClassBranch == "8-A");
    }

    [Fact]
    public async Task ImportingAgainReplacesResultsInsteadOfDuplicating()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS", "LGS", Day, null, 500), default);
        var first = s.LgsCsv((s.Ada, 10, 5, 300m));
        await s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, first, (await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, first, default)).Fingerprint, default);
        var second = s.LgsCsv((s.Ada, 12, 2, 350m));
        var preview = await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, second, default);
        preview.Accepted.Single().Replaces.Should().BeTrue();

        await s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, second, preview.Fingerprint, default);

        (await s.F.Db.ExamResults.SingleAsync()).Score.Should().Be(350m);
    }

    [Fact]
    public async Task BadRowsAreReportedWithTheirLineAndBlockTheImport()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS", "LGS", Day, 8, 500), default);
        var stranger = s.F.AddAccount("Student");
        var csv = s.LgsCsv((s.Ada, 15, 3, 400m), (s.Ada, 14, 3, 390m), (stranger, 10, 0, 200m), (s.Senior, 10, 0, 200m), (s.Bora, 10, 0, 600m))
            + $"\n{s.F.Accounts.Users[s.Cem].Email};400;15;10;10;0;10;0;5;0;5;0;5;0"
            + "\nbilinmiyor@example.test;100;5;0;5;0;5;0;5;0;5;0;5;0";

        var preview = await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, csv, default);

        preview.Errors.Select(x => x.Line).Should().Equal(3, 4, 5, 6, 7, 8);
        preview.Errors.Should().Contain(x => x.Line == 3 && x.Message.Contains("birden fazla"));
        preview.Errors.Should().Contain(x => x.Line == 5 && x.Message.Contains("8. sınıf"));
        preview.Errors.Should().Contain(x => x.Line == 6 && x.Message.Contains("puan"));
        preview.Errors.Should().Contain(x => x.Line == 7 && x.Message.Contains("Türkçe"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, csv, preview.Fingerprint, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, s.LgsCsv((s.Ada, 1, 1, 1m)), preview.Fingerprint, default));
        (await s.F.Db.ExamResults.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void TheTemplateNamesEveryColumnForTheExamScope() =>
        CoachingInstitutionExamService.Template("LGS").Should().Be("eposta;puan;TUR_dogru;TUR_yanlis;MAT_dogru;MAT_yanlis;FEN_dogru;FEN_yanlis;INK_dogru;INK_yanlis;DIN_dogru;DIN_yanlis;ENG_dogru;ENG_yanlis");

    [Fact]
    public void RanksShareTiesAndTopPercentIsRankOverParticipants()
    {
        var ranks = InstitutionExamRanking.Rank(new[] { (1, 30m), (2, 25m), (3, 25m), (4, 10m) });
        ranks.Select(x => (x.Key, x.Value.Rank, x.Value.TopPercent)).OrderBy(x => x.Key).Should().Equal((1, 1, 25m), (2, 2, 50m), (3, 2, 50m), (4, 4, 100m));
    }

    [Fact]
    public void InstitutionExamEndpointsAreForInstitutionManagers() =>
        typeof(CoachingInstitutionExamsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Roles == "SystemAdmin,InstitutionAdmin,InstitutionOwner");

    [Fact]
    public async Task StudentsSeeTheirOwnRankInTheirInstitutionsExams()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS denemesi", "LGS", Day, 8, 500), default);
        var csv = s.LgsCsv((s.Ada, 15, 3, 400m), (s.Bora, 18, 0, 470m), (s.Cem, 15, 3, 395m));
        await s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, csv, (await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, csv, default)).Fingerprint, default);

        var mine = (await s.Exams.GetStudentRanksAsync(s.Ada, default)).Should().ContainSingle().Subject;

        (mine.ExamId, mine.Title, mine.Net, mine.Rank, mine.Participants, mine.TopPercent).Should().Be((exam.Id, "LGS denemesi", 72m, 2, 3, 66.7m));
        (await s.Exams.GetStudentRanksAsync(s.Senior, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GradeFilteredReportsKeepExamsThatWereNotTiedToAGrade()
    {
        await using var s = await Setup.CreateAsync();
        var exam = Exam.Create(Guid.NewGuid(), "Sınıfsız deneme", ExamType.Mock, new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), 100, s.Institution);
        exam.AddResult(ExamResult.Create(exam.Id, s.Ada, 80));
        s.F.Db.Exams.Add(exam); await s.F.Db.SaveChangesAsync();

        var report = await new Coaching.Infrastructure.Repositories.CoachingComparativeReportRepository(s.F.Db).GetInstitutionComparisonAsync(
            s.Institution, [s.Ada], 8, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc), default);

        report.ExamResultCount.Should().Be(1, "the students are already filtered by grade");
    }

    [Fact]
    public void TeachersChooseTheGradeWhenTheyCreateAnExam()
    {
        Exam.Create(Guid.NewGuid(), "9. sınıf denemesi", ExamType.Mock, DateTime.UtcNow, 100, targetGradeLevel: 9).TargetGradeLevel.Should().Be(9);
        typeof(Coaching.Application.Commands.CreateExam.CreateExamCommand).GetProperty("TargetGradeLevel").Should().NotBeNull();
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public Guid Admin, Institution, Ada, Bora, Cem, Senior;
        public CoachingInstitutionExamService Exams => new(F.Db, F.Accounts, F.Store);

        public static async Task<Setup> CreateAsync()
        {
            var s = new Setup();
            s.Institution = Guid.NewGuid(); s.F.Accounts.Institutions.Add(s.Institution);
            s.Admin = s.F.AddAccount("InstitutionAdmin"); s.F.Accounts.Admins[s.Admin] = new(false, s.Institution);
            s.Ada = await s.Student(8, "8-A"); s.Bora = await s.Student(8, "8-B"); s.Cem = await s.Student(8, "8-A"); s.Senior = await s.Student(12, "12-C");
            return s;
        }

        private async Task<Guid> Student(int grade, string branch)
        {
            var id = F.AddAccount("Student");
            await F.Store.SetMembershipAsync(Admin, Institution, id, CoachingMemberRole.Student, true, default);
            var profile = await F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == id);
            profile.UpdateEducation(CoachingEducationStatus.Student, grade, null, null, null, null, branch, null);
            await F.Db.SaveChangesAsync();
            return id;
        }

        /// <summary>LGS rows: the same correct/wrong in every section, blanks are what is left.</summary>
        public string LgsCsv(params (Guid Student, int Correct, int Wrong, decimal Score)[] rows) =>
            CoachingInstitutionExamService.Template("LGS") + "\n" + string.Join("\n", rows.Select(row =>
                $"{F.Accounts.Users[row.Student].Email};{row.Score};" + string.Join(";", new[] { 20, 20, 20, 10, 10, 10 }.Select(count =>
                    $"{Math.Min(row.Correct, count)};{Math.Min(row.Wrong, Math.Max(0, count - Math.Min(row.Correct, count)))}"))));

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }
}

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

    // Roadmap O4 follow-up: an Excel (.xlsx) file with the template's columns is read like the CSV; line numbers are Excel rows.
    [Fact]
    public async Task AnExcelFileWithTheTemplatesColumnsIsReadLikeTheCsv()
    {
        await using var s = await Setup.CreateAsync();
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS denemesi", "LGS", Day, 8, 500), default);
        var csv = s.LgsCsv((s.Ada, 15, 3, 400m), (s.Bora, 18, 0, 470.5m));
        var xlsx = Workbook(csv.Split('\n').Select(line => line.Split(';')).Append(["yok@example.test", "300"]).ToArray());

        var text = InstitutionExamImportText.From(null, Convert.ToBase64String(xlsx));
        var preview = await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, text, default);

        preview.Accepted.Should().HaveCount(2);
        preview.Errors.Should().ContainSingle(x => x.Line == 4, "the unknown e-mail is on the fourth Excel row");
        InstitutionExamImportText.From("eposta;puan", null).Should().Be("eposta;puan");
        Assert.Throws<ArgumentException>(() => InstitutionExamImportText.From(null, Convert.ToBase64String([1, 2, 3, 4])));
        Assert.Throws<ArgumentException>(() => InstitutionExamImportText.From(null, "bu-base64-değil"));
        Assert.Throws<ArgumentException>(() => InstitutionExamImportText.From(null, null));
    }

    /// <summary>A one-sheet workbook; numeric-looking cells are numbers, the rest shared strings.</summary>
    private static byte[] Workbook(string[][] rows)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(stream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new();
            var strings = workbook.AddNewPart<DocumentFormat.OpenXml.Packaging.SharedStringTablePart>(); strings.SharedStringTable = new();
            var sheetPart = workbook.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
            var data = new DocumentFormat.OpenXml.Spreadsheet.SheetData();
            for (var r = 0; r < rows.Length; r++)
            {
                var row = new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = (uint)(r + 1) };
                for (var c = 0; c < rows[r].Length; c++)
                {
                    var value = rows[r][c];
                    var reference = (c >= 26 ? $"{(char)('A' + c / 26 - 1)}{(char)('A' + c % 26)}" : $"{(char)('A' + c)}") + (r + 1);
                    if (decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
                        row.Append(new DocumentFormat.OpenXml.Spreadsheet.Cell { CellReference = reference, CellValue = new(number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.Number });
                    else
                    {
                        strings.SharedStringTable.Append(new DocumentFormat.OpenXml.Spreadsheet.SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text(value)));
                        row.Append(new DocumentFormat.OpenXml.Spreadsheet.Cell { CellReference = reference, CellValue = new((strings.SharedStringTable.Count() - 1).ToString()),
                            DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString });
                    }
                }
                data.Append(row);
            }
            sheetPart.Worksheet = new(data);
            workbook.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets(new DocumentFormat.OpenXml.Spreadsheet.Sheet
                { Id = workbook.GetIdOfPart(sheetPart), SheetId = 1, Name = "Sonuçlar" }));
        }
        return stream.ToArray();
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

    // K2 (31.10.2026): the "Şube yok" group can be chosen as a filter and branches differing only in case are one group.
    [Fact]
    public async Task TheNoBranchGroupCanBeFilteredAndBranchCaseDoesNotSplitAGroup()
    {
        await using var s = await Setup.CreateAsync();
        var noBranch = await s.AddStudentAsync(8, null); var lowerCase = await s.AddStudentAsync(8, "8-a");
        var exam = await s.Exams.CreateAsync(s.Admin, s.Institution, new("LGS denemesi", "LGS", Day, 8, 500), default);
        var csv = s.LgsCsv((s.Ada, 15, 3, 400m), (s.Bora, 18, 0, 470m), (noBranch, 10, 2, 300m), (lowerCase, 12, 2, 350m));
        var preview = await s.Exams.PreviewImportAsync(s.Admin, s.Institution, exam.Id, csv, default);
        await s.Exams.ApplyImportAsync(s.Admin, s.Institution, exam.Id, csv, preview.Fingerprint, default);

        var report = await s.Exams.GetAsync(s.Admin, s.Institution, exam.Id, null, default);
        report.Branches.Select(x => (x.Branch, x.Participants)).Should().BeEquivalentTo(new[] { ("8-A", 2), ("8-B", 1), ("Şube yok", 1) });
        (await s.Exams.GetAsync(s.Admin, s.Institution, exam.Id, "Şube yok", default)).Results.Should().ContainSingle()
            .Which.StudentId.Should().Be(noBranch);
        (await s.Exams.GetAsync(s.Admin, s.Institution, exam.Id, "8-A", default)).Results.Select(x => x.StudentId)
            .Should().BeEquivalentTo(new[] { s.Ada, lowerCase });
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

        public Task<Guid> AddStudentAsync(int grade, string? branch) => Student(grade, branch);

        private async Task<Guid> Student(int grade, string? branch)
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

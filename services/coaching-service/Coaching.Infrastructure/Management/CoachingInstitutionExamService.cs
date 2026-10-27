using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record InstitutionExamCreate(string Title, string ExamCode, DateOnly ExamDate, int? GradeLevel, decimal MaxScore);
public sealed record InstitutionExamItem(Guid Id, string Title, string ExamCode, string ExamName, DateTime ExamDate, int? GradeLevel,
    decimal MaxScore, int Participants);
public sealed record InstitutionExamPage(IReadOnlyList<InstitutionExamItem> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record InstitutionExamRow(Guid StudentId, string StudentName, int? GradeLevel, string? ClassBranch, decimal Net, decimal Score,
    int Rank, decimal TopPercent, IReadOnlyList<PracticeSectionSummary> Sections);
public sealed record InstitutionExamGroup(string Branch, int Participants, decimal AverageNet, decimal BestNet);
public sealed record InstitutionExamSectionAverage(string Code, string Name, decimal AverageNet);
public sealed record InstitutionExamReport(InstitutionExamItem Exam, IReadOnlyList<InstitutionExamRow> Results,
    IReadOnlyList<InstitutionExamGroup> Branches, IReadOnlyList<InstitutionExamSectionAverage> Sections);
public sealed record InstitutionExamImportRow(int Line, Guid StudentId, string StudentName, decimal Net, bool Replaces);
public sealed record InstitutionExamImportError(int Line, string Message);
public sealed record InstitutionExamImportPreview(string Fingerprint, int Rows, IReadOnlyList<InstitutionExamImportRow> Accepted,
    IReadOnlyList<InstitutionExamImportError> Errors);
public sealed record StudentInstitutionExamRank(Guid ExamId, string Title, string ExamName, DateTime ExamDate, decimal Net, decimal Score,
    int Rank, int Participants, decimal TopPercent);
public readonly record struct InstitutionExamPlace(int Rank, decimal TopPercent);

/// <summary>Roadmap O4: equal nets share a rank (1, 2, 2, 4); top percent is rank / participants.</summary>
public static class InstitutionExamRanking
{
    public static Dictionary<TKey, InstitutionExamPlace> Rank<TKey>(IEnumerable<(TKey Key, decimal Net)> rows) where TKey : notnull
    {
        var ordered = rows.OrderByDescending(x => x.Net).ToArray();
        var places = new Dictionary<TKey, InstitutionExamPlace>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var rank = index > 0 && ordered[index].Net == ordered[index - 1].Net ? places[ordered[index - 1].Key].Rank : index + 1;
            places[ordered[index].Key] = new(rank, Math.Round(100m * rank / ordered.Length, 1, MidpointRounding.AwayFromZero));
        }
        return places;
    }
}

/// <summary>
/// Roadmap O4: an institution runs one practice exam for all its (grade's) students, imports the results from a CSV file
/// after a preview, and reads ranking, top percent, section averages and branch (şube) analysis.
/// </summary>
public sealed class CoachingInstitutionExamService(CoachingDbContext db, ICoachingAccountDirectory accounts, CoachingProfileStore profiles)
{
    private const int MaxRows = 5_000;
    private static readonly CultureInfo Turkish = new("tr-TR");

    public async Task<InstitutionExamItem> CreateAsync(Guid actor, Guid institutionId, InstitutionExamCreate request, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        var exam = Exam.CreateInstitutionWide(actor, institutionId, request.Title, request.ExamCode,
            request.ExamDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), request.MaxScore, request.GradeLevel);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(ct);
        return Item(exam, 0);
    }

    public async Task<InstitutionExamPage> ListAsync(Guid actor, Guid institutionId, int pageNumber, int pageSize, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Exams.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.CreatedByAdminId != null);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ExamDate).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new { Exam = x, Participants = x.Results.Count }).ToListAsync(ct);
        return new(rows.Select(x => Item(x.Exam, x.Participants)).ToArray(), total, pageNumber, pageSize);
    }

    public async Task<InstitutionExamReport> GetAsync(Guid actor, Guid institutionId, Guid examId, string? branch, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        var exam = await LoadAsync(institutionId, examId, ct);
        var studentIds = exam.Results.Select(x => x.StudentId).ToList();
        var studentProfiles = await db.CoachingStudentProfiles.AsNoTracking().Where(x => studentIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, ct);
        var names = await NamesAsync(studentIds, ct);
        var summaries = exam.Results.ToDictionary(x => x.StudentId, Summary);
        var places = InstitutionExamRanking.Rank(exam.Results.Select(x => (x.StudentId, Net(x, summaries))));
        var rows = exam.Results.Select(result =>
        {
            var profile = studentProfiles.GetValueOrDefault(result.StudentId);
            var place = places[result.StudentId];
            return new InstitutionExamRow(result.StudentId, names.GetValueOrDefault(result.StudentId) ?? "Öğrenci", profile?.GradeLevel,
                profile?.ClassBranch, Net(result, summaries), result.Score, place.Rank, place.TopPercent,
                summaries[result.StudentId]?.Sections.ToArray() ?? []);
        }).OrderBy(x => x.Rank).ThenBy(x => x.StudentName, StringComparer.Create(Turkish, true)).ToArray();
        var branches = rows.GroupBy(x => string.IsNullOrWhiteSpace(x.ClassBranch) ? "Şube yok" : x.ClassBranch!)
            .Select(group => new InstitutionExamGroup(group.Key, group.Count(), Math.Round(group.Average(x => x.Net), 2), group.Max(x => x.Net)))
            .OrderBy(x => x.Branch, StringComparer.Create(Turkish, true)).ToArray();
        var profile = Profile(exam);
        var sections = profile.Sections.Select(section =>
        {
            var nets = summaries.Values.Where(x => x is not null).SelectMany(x => x!.Sections).Where(x => x.SectionCode == section.Code).Select(x => x.Net).ToArray();
            return new InstitutionExamSectionAverage(section.Code, section.Name, nets.Length == 0 ? 0 : Math.Round(nets.Average(), 2));
        }).ToArray();
        var visible = string.IsNullOrWhiteSpace(branch) ? rows : rows.Where(x => string.Equals(x.ClassBranch, branch.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return new(Item(exam, rows.Length), visible, branches, sections);
    }

    /// <summary>The CSV header for an exam scope: e-mail, score and correct/wrong per section.</summary>
    public static string Template(string examCode)
    {
        var profile = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == examCode) ?? throw new ArgumentException("Geçerli bir deneme kapsamı seçin.");
        return string.Join(';', new[] { "eposta", "puan" }.Concat(profile.Sections.SelectMany(x => new[] { $"{x.Code}_dogru", $"{x.Code}_yanlis" })));
    }

    public async Task<InstitutionExamImportPreview> PreviewImportAsync(Guid actor, Guid institutionId, Guid examId, string csv, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        var exam = await LoadAsync(institutionId, examId, ct);
        var parsed = await ParseAsync(exam, institutionId, csv, ct);
        return new(Fingerprint(exam.Id, csv), parsed.Rows,
            parsed.Accepted.Select(x => new InstitutionExamImportRow(x.Line, x.StudentId, x.Name, x.Net, exam.Results.Any(r => r.StudentId == x.StudentId))).ToArray(),
            parsed.Errors);
    }

    /// <summary>Applies a previewed file. The file must be unchanged and free of errors; results are added or replaced.</summary>
    public async Task<int> ApplyImportAsync(Guid actor, Guid institutionId, Guid examId, string csv, string fingerprint, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institutionId, ct);
        var exam = await LoadAsync(institutionId, examId, ct, tracking: true);
        if (!string.Equals(Fingerprint(exam.Id, csv), fingerprint, StringComparison.Ordinal))
            throw new BusinessRuleException("Exam.ImportChanged", "Dosya önizlemeden sonra değişti. Yeniden önizleyin.");
        var parsed = await ParseAsync(exam, institutionId, csv, ct);
        if (parsed.Errors.Count > 0)
            throw new BusinessRuleException("Exam.ImportHasErrors", "Önce hatalı satırları düzeltip dosyayı yeniden yükleyin.");
        foreach (var row in parsed.Accepted) exam.ImportResult(row.StudentId, row.Score, row.Practice);
        await db.SaveChangesAsync(ct);
        return parsed.Accepted.Count;
    }

    /// <summary>The student's own place in each institution-wide exam they took.</summary>
    public async Task<IReadOnlyList<StudentInstitutionExamRank>> GetStudentRanksAsync(Guid studentId, CancellationToken ct)
    {
        var examIds = await db.ExamResults.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Exam.CreatedByAdminId != null).Select(x => x.ExamId).Distinct().ToListAsync(ct);
        if (examIds.Count == 0) return [];
        var exams = await db.Exams.AsNoTracking().Include(x => x.Results).Where(x => examIds.Contains(x.Id)).ToListAsync(ct);
        return exams.OrderByDescending(x => x.ExamDate).Select(exam =>
        {
            var summaries = exam.Results.ToDictionary(x => x.StudentId, Summary);
            var places = InstitutionExamRanking.Rank(exam.Results.Select(x => (x.StudentId, Net(x, summaries))));
            var mine = exam.Results.Single(x => x.StudentId == studentId);
            return new StudentInstitutionExamRank(exam.Id, exam.Title, Profile(exam).Name, exam.ExamDate, Net(mine, summaries), mine.Score,
                places[studentId].Rank, exam.Results.Count, places[studentId].TopPercent);
        }).ToArray();
    }

    private sealed record ParsedRow(int Line, Guid StudentId, string Name, decimal Score, PracticeExamInput Practice, decimal Net);
    private sealed record ParseResult(int Rows, List<ParsedRow> Accepted, List<InstitutionExamImportError> Errors);

    private async Task<ParseResult> ParseAsync(Exam exam, Guid institutionId, string csv, CancellationToken ct)
    {
        var profile = Profile(exam);
        var lines = (csv ?? "").TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var errors = new List<InstitutionExamImportError>(); var accepted = new List<ParsedRow>();
        var header = lines.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (header is null) { errors.Add(new(1, "Dosya boş.")); return new(0, accepted, errors); }
        var separator = header.Contains(';') ? ';' : ',';
        var columns = header.Split(separator).Select(Normalize).ToArray();
        int Column(params string[] names) => Array.FindIndex(columns, x => names.Contains(x));
        var emailColumn = Column("eposta", "e-posta", "email", "e-mail");
        var scoreColumn = Column("puan");
        var sectionColumns = profile.Sections.Select(x => (Section: x, Correct: Column($"{Normalize(x.Code)}_dogru"), Wrong: Column($"{Normalize(x.Code)}_yanlis"))).ToArray();
        var missing = new List<string>();
        if (emailColumn < 0) missing.Add("eposta");
        if (scoreColumn < 0) missing.Add("puan");
        missing.AddRange(sectionColumns.Where(x => x.Correct < 0 || x.Wrong < 0).Select(x => $"{x.Section.Code}_dogru/{x.Section.Code}_yanlis"));
        if (missing.Count > 0) { errors.Add(new(1, $"Eksik sütun: {string.Join(", ", missing)}. Şablonu kullanın.")); return new(0, accepted, errors); }

        var members = await db.CoachingInstitutionMemberships.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && x.Role == CoachingMemberRole.Student && x.IsActive).Select(x => x.UserId).ToListAsync(ct);
        var memberAccounts = await accounts.GetAccountsAsync(members, ct);
        var byEmail = memberAccounts.GroupBy(x => x.Email.Trim().ToLowerInvariant()).ToDictionary(x => x.Key, x => x.First());
        var grades = await db.CoachingStudentProfiles.AsNoTracking().Where(x => members.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, x => x.GradeLevel, ct);
        var seen = new HashSet<Guid>(); var rows = 0;
        var headerIndex = Array.IndexOf(lines, header);
        for (var index = headerIndex + 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            var line = index + 1; rows++;
            if (rows > MaxRows) { errors.Add(new(line, $"Bir dosyada en fazla {MaxRows} satır olabilir.")); break; }
            var cells = lines[index].Split(separator).Select(x => x.Trim().Trim('"')).ToArray();
            string Cell(int column) => column < cells.Length ? cells[column] : "";
            var email = Cell(emailColumn).ToLowerInvariant();
            if (!byEmail.TryGetValue(email, out var account)) { errors.Add(new(line, $"{(email.Length == 0 ? "E-posta boş" : email)}: öğrenci kurumda bulunamadı.")); continue; }
            if (!seen.Add(account.UserId)) { errors.Add(new(line, $"{email} dosyada birden fazla kez var.")); continue; }
            if (exam.TargetGradeLevel is { } target && grades.GetValueOrDefault(account.UserId) is { } grade && grade != target)
            { errors.Add(new(line, $"{email} {grade}. sınıfta; deneme {target}. sınıf için.")); continue; }
            if (!decimal.TryParse(Cell(scoreColumn).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var score) || score < 0 || score > exam.MaxScore)
            { errors.Add(new(line, $"{email}: puan 0 ile {exam.MaxScore.ToString("0.##", Turkish)} arasında olmalı.")); continue; }
            var answers = new List<PracticeSectionAnswers>(); string? problem = null;
            foreach (var (section, correctColumn, wrongColumn) in sectionColumns)
            {
                if (!int.TryParse(Cell(correctColumn), NumberStyles.None, CultureInfo.InvariantCulture, out var correct)
                    || !int.TryParse(Cell(wrongColumn), NumberStyles.None, CultureInfo.InvariantCulture, out var wrong))
                { problem = $"{section.Name}: doğru ve yanlış sayısı tam sayı olmalı."; break; }
                if (correct + wrong > section.QuestionCount)
                { problem = $"{section.Name}: doğru ve yanlış toplamı {section.QuestionCount} soruyu geçemez."; break; }
                answers.Add(new(section.Code, correct, wrong, section.QuestionCount - correct - wrong));
            }
            if (problem is not null) { errors.Add(new(line, $"{email}: {problem}")); continue; }
            var practice = new PracticeExamInput(profile.Code, answers);
            accepted.Add(new(line, account.UserId, $"{account.FirstName} {account.LastName}".Trim(), score, practice, PracticeExamRules.Summarize(practice).Net));
        }
        return new(rows, accepted, errors);
    }

    private async Task<Exam> LoadAsync(Guid institutionId, Guid examId, CancellationToken ct, bool tracking = false)
    {
        var query = tracking ? db.Exams : db.Exams.AsNoTracking();
        return await query.Include(x => x.Results)
            .SingleOrDefaultAsync(x => x.Id == examId && x.InstitutionId == institutionId && x.CreatedByAdminId != null, ct)
            ?? throw new KeyNotFoundException("Kurum denemesi bulunamadı.");
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : (await accounts.GetAccountsAsync(ids, ct)).ToDictionary(x => x.UserId, x => $"{x.FirstName} {x.LastName}".Trim());

    private static PracticeExamSummary? Summary(ExamResult result) =>
        result.GetPracticeExam() is { } practice ? PracticeExamRules.Summarize(practice) : null;

    private static decimal Net(ExamResult result, IReadOnlyDictionary<Guid, PracticeExamSummary?> summaries) =>
        summaries.GetValueOrDefault(result.StudentId)?.Net ?? result.Score;

    private static PracticeExamProfile Profile(Exam exam) =>
        PracticeExamRules.Profiles.Single(x => x.Code == exam.PracticeExamCode);

    private static InstitutionExamItem Item(Exam exam, int participants) =>
        new(exam.Id, exam.Title, exam.PracticeExamCode!, Profile(exam).Name, exam.ExamDate, exam.TargetGradeLevel, exam.MaxScore, participants);

    private static string Normalize(string value) => value.Trim().Trim('"').ToLowerInvariant()
        .Replace('ğ', 'g').Replace('ı', 'i').Replace('ş', 's').Replace('ç', 'c').Replace('ö', 'o').Replace('ü', 'u').Replace('İ', 'i');

    private static string Fingerprint(Guid examId, string csv) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(examId.ToString("N") + "\n" + (csv ?? "").Replace("\r\n", "\n"))));
}

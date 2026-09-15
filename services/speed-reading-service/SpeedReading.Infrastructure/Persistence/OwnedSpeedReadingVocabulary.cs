using System.Text;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Vocabulary;
using SpeedReading.Domain.Vocabulary;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingVocabulary(OwnedSpeedReadingDbContext db) : ISpeedReadingVocabulary
{
    private const string CreateScope = "speed-reading.vocabulary.create";
    private const string UpdateScope = "speed-reading.vocabulary.update";
    private const string DeleteScope = "speed-reading.vocabulary.delete";

    public async Task<VocabularyPage> GetItemsAsync(string? search, string? category, int? difficultyLevel, Guid? ageGroupId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.VocabularyItems.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.Word.Contains(term) || item.Definition.Contains(term) || (item.ExampleSentence != null && item.ExampleSentence.Contains(term))); }
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category.Trim());
        if (difficultyLevel.HasValue) query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        if (ageGroupId.HasValue) query = query.Where(item => item.TargetAgeGroupId == ageGroupId.Value);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.DifficultyLevel).ThenBy(item => item.Word).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ageNames = await GetAgeNamesAsync(rows.Select(item => item.TargetAgeGroupId), cancellationToken);
        return new VocabularyPage(rows.Select(item => ToSummary(item, ageNames)).ToList(), total, pageNumber, pageSize, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<VocabularyItemSummary?> GetItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.VocabularyItems.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return null;
        return ToSummary(item, await GetAgeNamesAsync([item.TargetAgeGroupId], cancellationToken));
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        db.VocabularyItems.AsNoTracking().Where(item => !item.IsDeleted).Select(item => item.Category).Distinct().OrderBy(item => item).ToListAsync(cancellationToken).ContinueWith(task => (IReadOnlyList<string>)task.Result, cancellationToken);

    public async Task<VocabularyItemSummary> CreateItemAsync(
        VocabularyItemRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return await GetItemAsync(existing.ResourceId, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency kaydına ait kelime bulunamadı; yeni bir anahtar kullanın.");
        }

        Validate(request); await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var now = DateTime.UtcNow;
        var item = VocabularyItem.Create(Guid.NewGuid(), request.Word, request.Definition, request.ExampleSentence, request.Synonyms, request.Antonyms,
            request.Category, request.DifficultyLevel, request.TargetAgeGroupId, actorId, now);
        db.VocabularyItems.Add(item);
        OwnedContentMutationIdempotency.Add(db, CreateScope, key, requestHash, item.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, CreateScope, key, cancellationToken);
        if (concurrent is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
            return await GetItemAsync(concurrent.ResourceId, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency kaydına ait kelime bulunamadı; yeni bir anahtar kullanın.");
        }
        return await GetItemAsync(item.Id, cancellationToken) ?? throw new InvalidOperationException("Vocabulary item could not be read after creation.");
    }

    public async Task<VocabularyItemSummary?> UpdateItemAsync(
        Guid id,
        VocabularyItemRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, UpdateScope, id, request);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return await GetItemAsync(id, cancellationToken);
        }

        Validate(request); await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var item = await db.VocabularyItems.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return null;
        var now = DateTime.UtcNow;
        item.Update(request.Word, request.Definition, request.ExampleSentence, request.Synonyms, request.Antonyms, request.Category,
            request.DifficultyLevel, request.TargetAgeGroupId, actorId, now);
        OwnedContentMutationIdempotency.Add(db, UpdateScope, key, requestHash, item.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, UpdateScope, key, cancellationToken);
        if (concurrent is not null)
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
        return await GetItemAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteItemAsync(
        Guid id,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, DeleteScope, id);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return true;
        }

        var item = await db.VocabularyItems.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return false;
        var now = DateTime.UtcNow;
        item.Delete(actorId, now);
        OwnedContentMutationIdempotency.Add(db, DeleteScope, key, requestHash, item.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, DeleteScope, key, cancellationToken);
        if (concurrent is not null)
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
        return true;
    }

    public async Task<IReadOnlyList<UserVocabularySummary>> GetUserVocabularyAsync(Guid userId, int? status, CancellationToken cancellationToken)
    {
        var query = from progress in db.UserVocabularyProgresses.AsNoTracking()
                    join item in db.VocabularyItems.AsNoTracking() on progress.VocabularyItemId equals item.Id
                    where progress.UserId == userId && !progress.IsDeleted && !item.IsDeleted
                    select new { Progress = progress, Item = item };
        if (status.HasValue) query = query.Where(value => value.Progress.Box == status.Value);
        var rows = await query.OrderBy(value => value.Progress.NextReviewDate).ToListAsync(cancellationToken);
        var ageNames = await GetAgeNamesAsync(rows.Select(value => value.Item.TargetAgeGroupId), cancellationToken);
        return rows.Select(value => ToUserSummary(value.Progress, value.Item, ageNames)).ToList();
    }

    public async Task<UserVocabularySummary?> AddToUserVocabularyAsync(Guid userId, Guid vocabularyItemId, CancellationToken cancellationToken)
    {
        var item = await db.VocabularyItems.AsNoTracking().SingleOrDefaultAsync(value => value.Id == vocabularyItemId && !value.IsDeleted, cancellationToken);
        if (item is null) return null;
        var progress = await db.UserVocabularyProgresses.OrderByDescending(value => value.CreatedAt).FirstOrDefaultAsync(value => value.UserId == userId && value.VocabularyItemId == vocabularyItemId, cancellationToken);
        var now = DateTime.UtcNow;
        if (progress is null) { progress = UserVocabularyProgress.Create(Guid.NewGuid(), userId, vocabularyItemId, now); db.UserVocabularyProgresses.Add(progress); }
        else if (progress.IsDeleted) progress.Reactivate(userId, now);
        await db.SaveChangesAsync(cancellationToken);
        return ToUserSummary(progress, item, await GetAgeNamesAsync([item.TargetAgeGroupId], cancellationToken));
    }

    public async Task<bool> UpdateUserVocabularyAsync(Guid userId, Guid progressId, bool isCorrect, CancellationToken cancellationToken)
    {
        var progress = await db.UserVocabularyProgresses.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == progressId && value.UserId == userId && !value.IsDeleted, cancellationToken);
        if (progress is null)
            return false;

        var review = await OwnedVocabularyProgressRecorder.RecordAsync(
            db,
            userId,
            progress.VocabularyItemId,
            isCorrect,
            DateTime.UtcNow,
            cancellationToken);
        if (!review.Found)
            return false;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserVocabularySummary>> GetDueForReviewAsync(Guid userId, CancellationToken cancellationToken)
    {
        var values = await GetUserVocabularyAsync(userId, null, cancellationToken); return values.Where(item => item.NextReviewAt <= DateTime.UtcNow).ToList();
    }

    public async Task<VocabularyImportResult> ImportCsvAsync(Stream csv, Guid actorId, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, true, leaveOpen: true);
        var header = await reader.ReadLineAsync(cancellationToken); if (string.IsNullOrWhiteSpace(header)) return new VocabularyImportResult(0, 0, []);
        var headers = ParseCsvLine(header).Select((value, index) => new { Name = value.Trim(), Index = index }).Where(value => value.Name.Length > 0).ToDictionary(value => value.Name, value => value.Index, StringComparer.OrdinalIgnoreCase);
        var success = 0; var failure = 0; var errors = new List<string>(); var row = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            row++; if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var values = ParseCsvLine(line); var word = GetValue(headers, values, "Word"); var definition = GetValue(headers, values, "Definition"); var category = GetValue(headers, values, "Category");
                var difficultyText = GetValue(headers, values, "DifficultyLevel"); var difficulty = string.IsNullOrWhiteSpace(difficultyText) ? 1 : int.TryParse(difficultyText, out var parsed) ? parsed : throw new FormatException("DifficultyLevel must be an integer.");
                Guid? ageGroupId = null; var ageName = GetValue(headers, values, "TargetAgeGroup");
                if (ageName.Length > 0) { ageGroupId = await db.AgeGroupConfigurations.Where(item => item.Name == ageName || item.DisplayName == ageName).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken); if (!ageGroupId.HasValue) throw new FormatException($"Unknown TargetAgeGroup '{ageName}'."); }
                var request = new VocabularyItemRequest(word, definition, GetValue(headers, values, "ExampleSentence"), GetValue(headers, values, "Synonyms"), GetValue(headers, values, "Antonyms"), category, difficulty, ageGroupId);
                Validate(request); var item = VocabularyItem.Create(Guid.NewGuid(), request.Word, request.Definition, request.ExampleSentence, request.Synonyms, request.Antonyms, request.Category, request.DifficultyLevel, request.TargetAgeGroupId, actorId, DateTime.UtcNow); db.VocabularyItems.Add(item); success++;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                failure++;
                errors.Add($"Row {row}: {exception.Message}");
            }
        }
        if (success > 0) await db.SaveChangesAsync(cancellationToken); return new VocabularyImportResult(success, failure, errors);
    }

    public async Task<byte[]> ExportCsvAsync(string? category, int? difficultyLevel, Guid? ageGroupId, CancellationToken cancellationToken)
    {
        var query = db.VocabularyItems.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category.Trim());
        if (difficultyLevel.HasValue) query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        if (ageGroupId.HasValue) query = query.Where(item => item.TargetAgeGroupId == ageGroupId.Value);
        var rows = await query.OrderBy(item => item.Category).ThenBy(item => item.DifficultyLevel).ThenBy(item => item.Word).ToListAsync(cancellationToken);
        var names = await GetAgeNamesAsync(rows.Select(item => item.TargetAgeGroupId), cancellationToken); var builder = new StringBuilder(); builder.AppendLine("Word,Definition,ExampleSentence,Synonyms,Antonyms,Category,DifficultyLevel,TargetAgeGroup");
        foreach (var item in rows) builder.AppendLine(string.Join(',', Csv(item.Word), Csv(item.Definition), Csv(item.ExampleSentence), Csv(item.Synonyms), Csv(item.Antonyms), Csv(item.Category), item.DifficultyLevel, Csv(item.TargetAgeGroupId.HasValue && names.TryGetValue(item.TargetAgeGroupId.Value, out var name) ? name : string.Empty)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? id, CancellationToken cancellationToken) { if (id.HasValue && !await db.AgeGroupConfigurations.AnyAsync(item => item.Id == id.Value, cancellationToken)) throw new KeyNotFoundException("Target age group not found."); }
    private async Task<Dictionary<Guid, string>> GetAgeNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken) { var values = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray(); return values.Length == 0 ? [] : await db.AgeGroupConfigurations.AsNoTracking().Where(item => values.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken); }
    private static VocabularyItemSummary ToSummary(VocabularyItem item, IReadOnlyDictionary<Guid, string> names) => new(item.Id, item.Word, item.Definition, item.ExampleSentence, item.Synonyms, item.Antonyms, item.TargetAgeGroupId, item.TargetAgeGroupId.HasValue && names.TryGetValue(item.TargetAgeGroupId.Value, out var name) ? name : null, item.DifficultyLevel, item.Category, item.CreatedAt, item.UpdatedAt);
    private static UserVocabularySummary ToUserSummary(UserVocabularyProgress progress, VocabularyItem item, IReadOnlyDictionary<Guid, string> names) => new(progress.Id, progress.UserId, progress.VocabularyItemId, Math.Clamp(progress.Box, 1, 5), progress.ConsecutiveCorrectCount, 0, progress.LastReviewedAt, progress.NextReviewDate, progress.CreatedAt, ToSummary(item, names));
    private static void Validate(VocabularyItemRequest request) { if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Definition) || string.IsNullOrWhiteSpace(request.Category)) throw new ArgumentException("Word, Definition and Category are required."); if (request.DifficultyLevel is < 1 or > 5) throw new ArgumentException("DifficultyLevel must be between 1 and 5."); }
    private static string GetValue(IReadOnlyDictionary<string, int> headers, IReadOnlyList<string> values, string name) => headers.TryGetValue(name, out var index) && index < values.Count ? values[index].Trim() : string.Empty;
    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    private static List<string> ParseCsvLine(string line) { var values = new List<string>(); var builder = new StringBuilder(); var quoted = false; for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { builder.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { values.Add(builder.ToString()); builder.Clear(); } else builder.Append(c); } values.Add(builder.ToString()); return values; }
}

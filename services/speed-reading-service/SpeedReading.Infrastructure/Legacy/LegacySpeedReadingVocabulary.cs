using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Vocabulary;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingVocabulary(SpeedReadingDbContext db) : ISpeedReadingVocabulary
{
    public async Task<VocabularyPage> GetItemsAsync(
        string? search,
        string? category,
        int? difficultyLevel,
        Guid? ageGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.VocabularyItems
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(item => item.Word.Contains(search) ||
                item.Definition.Contains(search) ||
                (item.ExampleSentence != null && item.ExampleSentence.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category.Trim());
        }

        if (difficultyLevel.HasValue)
        {
            query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        }

        if (ageGroupId.HasValue)
        {
            query = query.Where(item => item.TargetAgeGroupConfigurationId == ageGroupId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Word)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(VocabularyRowSelector)
            .ToListAsync(cancellationToken);

        var ageNames = await GetAgeNamesAsync(rows.Select(item => item.TargetAgeGroupId), cancellationToken);
        return new VocabularyPage(
            rows.Select(item => ToSummary(item, ageNames)).ToList(),
            totalCount,
            pageNumber,
            pageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<VocabularyItemSummary?> GetItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.VocabularyItems
            .AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(VocabularyRowSelector)
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var ageNames = await GetAgeNamesAsync([row.TargetAgeGroupId], cancellationToken);
        return ToSummary(row, ageNames);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        return await db.VocabularyItems
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);
    }

    public async Task<VocabularyItemSummary> CreateItemAsync(
        VocabularyItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateItemRequest(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);

        var item = new LegacyVocabularyItem
        {
            Id = Guid.NewGuid(),
            Word = request.Word.Trim(),
            Definition = request.Definition.Trim(),
            ExampleSentence = NormalizeOptional(request.ExampleSentence),
            Synonyms = NormalizeOptional(request.Synonyms),
            Antonyms = NormalizeOptional(request.Antonyms),
            Category = request.Category.Trim(),
            DifficultyLevel = request.DifficultyLevel,
            TargetAgeGroupConfigurationId = request.TargetAgeGroupId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };

        db.VocabularyItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return await GetItemAsync(item.Id, cancellationToken) ?? throw new InvalidOperationException("Vocabulary item could not be read after creation.");
    }

    public async Task<VocabularyItemSummary?> UpdateItemAsync(
        Guid id,
        VocabularyItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateItemRequest(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);

        var item = await db.VocabularyItems
            .SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.Word = request.Word.Trim();
        item.Definition = request.Definition.Trim();
        item.ExampleSentence = NormalizeOptional(request.ExampleSentence);
        item.Synonyms = NormalizeOptional(request.Synonyms);
        item.Antonyms = NormalizeOptional(request.Antonyms);
        item.Category = request.Category.Trim();
        item.DifficultyLevel = request.DifficultyLevel;
        item.TargetAgeGroupConfigurationId = request.TargetAgeGroupId;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = actorId;

        await db.SaveChangesAsync(cancellationToken);
        return await GetItemAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteItemAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var item = await db.VocabularyItems
            .SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserVocabularySummary>> GetUserVocabularyAsync(
        Guid userId,
        int? status,
        CancellationToken cancellationToken)
    {
        var query = from progress in db.UserVocabularyProgresses.AsNoTracking()
                    join item in db.VocabularyItems.AsNoTracking()
                        on progress.VocabularyItemId equals item.Id
                    where progress.UserId == userId && !progress.IsDeleted && !item.IsDeleted
                    select new UserVocabularyRow(progress, item);

        if (status.HasValue)
        {
            query = query.Where(item => item.Progress.Box == status.Value);
        }

        var rows = await query
            .OrderBy(item => item.Progress.NextReviewDate)
            .ToListAsync(cancellationToken);
        var ageNames = await GetAgeNamesAsync(rows.Select(item => item.Item.TargetAgeGroupConfigurationId), cancellationToken);

        return rows.Select(item => ToUserSummary(item.Progress, ToVocabularyRow(item.Item), ageNames)).ToList();
    }

    public async Task<UserVocabularySummary?> AddToUserVocabularyAsync(
        Guid userId,
        Guid vocabularyItemId,
        CancellationToken cancellationToken)
    {
        var item = await db.VocabularyItems
            .AsNoTracking()
            .Where(value => value.Id == vocabularyItemId && !value.IsDeleted)
            .Select(VocabularyRowSelector)
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var progress = await db.UserVocabularyProgresses
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(value => value.UserId == userId && value.VocabularyItemId == vocabularyItemId, cancellationToken);
        var now = DateTime.UtcNow;

        if (progress is null)
        {
            progress = new LegacyUserVocabularyProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VocabularyItemId = vocabularyItemId,
                Box = 1,
                ConsecutiveCorrectCount = 0,
                LastReviewedAt = now,
                NextReviewDate = now,
                CreatedAt = now,
                CreatedBy = userId
            };
            db.UserVocabularyProgresses.Add(progress);
        }
        else if (progress.IsDeleted)
        {
            progress.IsDeleted = false;
            progress.DeletedAt = null;
            progress.DeletedBy = null;
            progress.UpdatedAt = now;
            progress.UpdatedBy = userId;
        }

        await db.SaveChangesAsync(cancellationToken);
        var ageNames = await GetAgeNamesAsync([item.TargetAgeGroupId], cancellationToken);
        return ToUserSummary(progress, item, ageNames);
    }

    public async Task<bool> UpdateUserVocabularyAsync(
        Guid userId,
        Guid progressId,
        bool isCorrect,
        CancellationToken cancellationToken)
    {
        var progress = await db.UserVocabularyProgresses
            .SingleOrDefaultAsync(value => value.Id == progressId && value.UserId == userId && !value.IsDeleted, cancellationToken);
        if (progress is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        progress.LastReviewedAt = now;
        progress.Box = Math.Clamp(progress.Box, 1, 5);
        if (isCorrect)
        {
            progress.ConsecutiveCorrectCount++;
            progress.Box = Math.Min(5, progress.Box + 1);
            progress.NextReviewDate = now.AddDays(progress.Box switch
            {
                2 => 3,
                3 => 7,
                4 => 14,
                5 => 30,
                _ => 1
            });
        }
        else
        {
            progress.ConsecutiveCorrectCount = 0;
            progress.Box = 1;
            progress.NextReviewDate = now;
        }

        progress.UpdatedAt = now;
        progress.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserVocabularySummary>> GetDueForReviewAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var vocabulary = await GetUserVocabularyAsync(userId, null, cancellationToken);
        return vocabulary.Where(item => item.NextReviewAt <= DateTime.UtcNow).ToList();
    }

    public async Task<VocabularyImportResult> ImportCsvAsync(
        Stream csv,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new VocabularyImportResult(0, 0, []);
        }

        var headers = ParseCsvLine(headerLine)
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var successCount = 0;
        var failureCount = 0;
        var errors = new List<string>();
        var rowNumber = 1;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var values = ParseCsvLine(line);
                var word = GetValue(headers, values, "Word");
                var definition = GetValue(headers, values, "Definition");
                var category = GetValue(headers, values, "Category");
                if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(definition) || string.IsNullOrWhiteSpace(category))
                {
                    throw new FormatException("Word, Definition and Category are required.");
                }

                var difficultyText = GetValue(headers, values, "DifficultyLevel");
                var difficulty = string.IsNullOrWhiteSpace(difficultyText) ? 1 :
                    int.TryParse(difficultyText, out var parsedDifficulty)
                        ? parsedDifficulty
                        : throw new FormatException("DifficultyLevel must be an integer.");
                var ageName = GetValue(headers, values, "TargetAgeGroup");
                Guid? ageGroupId = null;
                if (!string.IsNullOrWhiteSpace(ageName))
                {
                    ageGroupId = await db.AgeGroupConfigurations
                        .Where(item => item.Name == ageName || item.DisplayName == ageName)
                        .Select(item => (Guid?)item.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (!ageGroupId.HasValue)
                    {
                        throw new FormatException($"Unknown TargetAgeGroup '{ageName}'.");
                    }
                }

                ValidateItemRequest(new VocabularyItemRequest(
                    word,
                    definition,
                    GetValue(headers, values, "ExampleSentence"),
                    GetValue(headers, values, "Synonyms"),
                    GetValue(headers, values, "Antonyms"),
                    category,
                    difficulty,
                    ageGroupId));

                db.VocabularyItems.Add(new LegacyVocabularyItem
                {
                    Id = Guid.NewGuid(),
                    Word = word.Trim(),
                    Definition = definition.Trim(),
                    ExampleSentence = NormalizeOptional(GetValue(headers, values, "ExampleSentence")),
                    Synonyms = NormalizeOptional(GetValue(headers, values, "Synonyms")),
                    Antonyms = NormalizeOptional(GetValue(headers, values, "Antonyms")),
                    Category = category.Trim(),
                    DifficultyLevel = difficulty,
                    TargetAgeGroupConfigurationId = ageGroupId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorId
                });
                successCount++;
            }
            catch (FormatException exception)
            {
                failureCount++;
                errors.Add($"Row {rowNumber}: {exception.Message}");
            }
        }

        if (successCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new VocabularyImportResult(successCount, failureCount, errors);
    }

    public async Task<byte[]> ExportCsvAsync(
        string? category,
        int? difficultyLevel,
        Guid? ageGroupId,
        CancellationToken cancellationToken)
    {
        var query = db.VocabularyItems
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category.Trim());
        }
        if (difficultyLevel.HasValue)
        {
            query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        }
        if (ageGroupId.HasValue)
        {
            query = query.Where(item => item.TargetAgeGroupConfigurationId == ageGroupId.Value);
        }

        var rows = await query
            .OrderBy(item => item.Category)
            .ThenBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Word)
            .Select(VocabularyRowSelector)
            .ToListAsync(cancellationToken);
        var ageNames = await GetAgeNamesAsync(rows.Select(item => item.TargetAgeGroupId), cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Word,Definition,ExampleSentence,Synonyms,Antonyms,Category,DifficultyLevel,TargetAgeGroup");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.Word),
                Csv(row.Definition),
                Csv(row.ExampleSentence),
                Csv(row.Synonyms),
                Csv(row.Antonyms),
                Csv(row.Category),
                row.DifficultyLevel,
                Csv(row.TargetAgeGroupId.HasValue && ageNames.TryGetValue(row.TargetAgeGroupId.Value, out var name) ? name : string.Empty)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? ageGroupId, CancellationToken cancellationToken)
    {
        if (ageGroupId.HasValue && !await db.AgeGroupConfigurations.AnyAsync(item => item.Id == ageGroupId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("Target age group not found.");
        }
    }

    private async Task<Dictionary<Guid, string>> GetAgeNamesAsync(
        IEnumerable<Guid?> ageGroupIds,
        CancellationToken cancellationToken)
    {
        var ids = ageGroupIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
    }

    private static readonly Expression<Func<LegacyVocabularyItem, VocabularyRow>> VocabularyRowSelector =
        item => new VocabularyRow
        {
            Id = item.Id,
            Word = item.Word,
            Definition = item.Definition,
            ExampleSentence = item.ExampleSentence,
            Synonyms = item.Synonyms,
            Antonyms = item.Antonyms,
            TargetAgeGroupId = item.TargetAgeGroupConfigurationId,
            DifficultyLevel = item.DifficultyLevel,
            Category = item.Category,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    private static VocabularyRow ToVocabularyRow(LegacyVocabularyItem item) => new()
    {
        Id = item.Id,
        Word = item.Word,
        Definition = item.Definition,
        ExampleSentence = item.ExampleSentence,
        Synonyms = item.Synonyms,
        Antonyms = item.Antonyms,
        TargetAgeGroupId = item.TargetAgeGroupConfigurationId,
        DifficultyLevel = item.DifficultyLevel,
        Category = item.Category,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static VocabularyItemSummary ToSummary(VocabularyRow row, IReadOnlyDictionary<Guid, string> ageNames) =>
        new(
            row.Id,
            row.Word,
            row.Definition,
            row.ExampleSentence,
            row.Synonyms,
            row.Antonyms,
            row.TargetAgeGroupId,
            row.TargetAgeGroupId.HasValue && ageNames.TryGetValue(row.TargetAgeGroupId.Value, out var name) ? name : null,
            row.DifficultyLevel,
            row.Category,
            row.CreatedAt,
            row.UpdatedAt);

    private static UserVocabularySummary ToUserSummary(
        LegacyUserVocabularyProgress progress,
        VocabularyRow item,
        IReadOnlyDictionary<Guid, string> ageNames)
    {
        return new UserVocabularySummary(
            progress.Id,
            progress.UserId,
            progress.VocabularyItemId,
            Math.Clamp(progress.Box, 1, 5),
            progress.ConsecutiveCorrectCount,
            0,
            progress.LastReviewedAt,
            progress.NextReviewDate,
            progress.CreatedAt,
            ToSummary(item, ageNames));
    }

    private static void ValidateItemRequest(VocabularyItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Definition) ||
            string.IsNullOrWhiteSpace(request.Category))
        {
            throw new ArgumentException("Word, Definition and Category are required.");
        }

        if (request.DifficultyLevel is < 1 or > 5)
        {
            throw new ArgumentException("DifficultyLevel must be between 1 and 5.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetValue(IReadOnlyDictionary<string, int> headers, IReadOnlyList<string> values, string name) =>
        headers.TryGetValue(name, out var index) && index < values.Count ? values[index].Trim() : string.Empty;

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(character);
            }
        }

        values.Add(builder.ToString());
        return values;
    }

    private sealed class VocabularyRow
    {
        public Guid Id { get; init; }
        public string Word { get; init; } = string.Empty;
        public string Definition { get; init; } = string.Empty;
        public string? ExampleSentence { get; init; }
        public string? Synonyms { get; init; }
        public string? Antonyms { get; init; }
        public Guid? TargetAgeGroupId { get; init; }
        public int DifficultyLevel { get; init; }
        public string Category { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed record UserVocabularyRow(
        LegacyUserVocabularyProgress Progress,
        LegacyVocabularyItem Item);
}

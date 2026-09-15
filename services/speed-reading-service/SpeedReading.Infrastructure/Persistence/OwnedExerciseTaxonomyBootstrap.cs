using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedExerciseTaxonomyBootstrapResult(
    int CategoriesCreated,
    int ExerciseTypesClassified,
    int AlreadyClassified,
    int UnclassifiedTypes,
    DateTime CompletedAtUtc);

/// <summary>
/// Explicit, idempotent maintenance action for catalog rows created before the
/// exercise taxonomy existed. It only fills missing category links.
/// </summary>
public sealed class OwnedExerciseTaxonomyBootstrap(OwnedSpeedReadingDbContext db)
{
    private static readonly Guid SystemActorId = new("d718b98b-2ff3-4c2d-8cf3-21c64d6f75ad");

    public async Task<OwnedExerciseTaxonomyBootstrapResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var categoriesByName = await db.ExerciseTypeCategories
            .Where(item => !item.IsDeleted)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categoriesCreated = 0;
        foreach (var definition in ExerciseTaxonomy.GetCategories())
        {
            if (categoriesByName.ContainsKey(definition.Key))
                continue;
            var category = ExerciseTypeCategory.Import(
                Guid.NewGuid(),
                definition.Key,
                definition.DisplayName,
                definition.Description,
                definition.SortOrder,
                isActive: true,
                now,
                SystemActorId.ToString(),
                null,
                null);
            db.ExerciseTypeCategories.Add(category);
            categoriesByName.Add(definition.Key, category);
            categoriesCreated++;
        }

        var types = await db.ExerciseTypes
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var classified = 0;
        var alreadyClassified = 0;
        var unclassified = 0;
        foreach (var type in types)
        {
            if (type.CategoryId.HasValue)
            {
                alreadyClassified++;
                continue;
            }

            string categoryKey;
            try
            {
                categoryKey = ExerciseTaxonomy.ResolveCategoryKey(type.EngineType);
            }
            catch (ArgumentException)
            {
                unclassified++;
                continue;
            }

            var category = categoriesByName[categoryKey];
            type.Update(
                type.Name,
                type.DisplayName,
                ExerciseConfigurationRules.NormalizeEngineType(type.EngineType),
                category.Id,
                type.Description,
                type.IconName,
                type.ColorCode,
                type.SortOrder,
                type.IsActive,
                SystemActorId,
                now);
            classified++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new OwnedExerciseTaxonomyBootstrapResult(
            categoriesCreated,
            classified,
            alreadyClassified,
            unclassified,
            now);
    }
}

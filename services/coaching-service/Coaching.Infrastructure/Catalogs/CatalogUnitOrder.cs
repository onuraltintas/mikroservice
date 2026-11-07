using Coaching.Infrastructure.Data;

namespace Coaching.Infrastructure.Catalogs;

/// <summary>A catalog unit's place in the curriculum (see <see cref="CatalogUnitOrder"/>).</summary>
public sealed class CatalogUnitRank
{
    public Guid UnitId { get; init; }
    /// <summary>1 for a unit without any rank (it comes after the ranked ones), else 0.</summary>
    public int Unranked { get; init; }
    public int? Rank { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// G5 (#42): the curriculum order of catalog units, shared by the catalog (Başlangıç durumum), the yearly plan and the practice
/// advice. The approved catalog's units have no DisplayOrder of their own (all 768 in the snapshot of 01.11.2026); their upper
/// topics carry lesson-wide curriculum ranks (TYT Matematik: Denklem ve Eşitsizlikler 1–5, Mantık 6, Kümeler 7–8, …, Uzay
/// Geometri 26). The rule: the unit's own rank, else its first upper topic's rank; units without any rank last, by name. Join
/// on <see cref="CatalogUnitRank.UnitId"/> and order by Unranked, Rank, Name (then the unit id).
/// </summary>
public static class CatalogUnitOrder
{
    public static IQueryable<CatalogUnitRank> Ranks(CoachingDbContext db) =>
        db.StudyCatalogUnits
            .Select(u => new { u.Id, u.Name, Rank = u.DisplayOrder ?? db.StudyCatalogTopics.Where(x => x.UnitId == u.Id && x.ParentId == null).Min(x => x.DisplayOrder) })
            .Select(x => new CatalogUnitRank { UnitId = x.Id, Unranked = x.Rank == null ? 1 : 0, Rank = x.Rank, Name = x.Name });
}

using Identity.Application.Registration;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

/// <summary>Roadmap O11: which campaigns brought the registrations of a product in a period.</summary>
public sealed class RegistrationSourceReport(IdentityDbContext db)
{
    public async Task<RegistrationSourceSummary> GetAsync(PlatformProduct product, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var total = await db.UserProductAccesses.AsNoTracking().CountAsync(access => access.Product == product
            && (access.Source == UserProductAccessSource.SelfRegistration || access.Source == UserProductAccessSource.GoogleRegistration)
            && access.GrantedAt >= from && access.GrantedAt < to, ct);
        var rows = (await db.RegistrationSources.AsNoTracking()
                .Where(source => source.Product == product && source.CreatedAt >= from && source.CreatedAt < to)
                .GroupBy(source => new { source.UtmSource, source.UtmMedium, source.UtmCampaign })
                .Select(group => new { group.Key.UtmSource, group.Key.UtmMedium, group.Key.UtmCampaign, Count = group.Count() })
                .ToListAsync(ct))
            .OrderByDescending(row => row.Count).ThenBy(row => row.UtmSource).Take(200)
            .Select(row => new RegistrationSourceRow(row.UtmSource, row.UtmMedium, row.UtmCampaign, row.Count)).ToArray();
        return new(from, to, total, Math.Max(0, total - rows.Sum(row => row.Count)), rows);
    }
}

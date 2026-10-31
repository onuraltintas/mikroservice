using Coaching.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class SpeedReadingParentSummaryClient(HttpClient http, IConfiguration configuration) : ISpeedReadingParentSummaryClient
{
    public Task<SpeedReadingParentWeek?> GetWeekAsync(Guid studentUserId, DateOnly weekStart, CancellationToken ct) => throw new NotImplementedException();
}

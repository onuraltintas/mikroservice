using System.Text.Json;

namespace SpeedReading.Domain.Sessions;

// Fixed pilot assessment blocks, not validated age norms or clinical measurements.
public sealed class AssessmentBlockProtocol
{
    public int Version { get; set; } = 2;
    public int PracticeRounds { get; set; } = 3;
    public int RoundsPerBlock { get; set; }
    public int[] BlockValues { get; set; } = [];
    public int TotalRounds => PracticeRounds + RoundsPerBlock * BlockValues.Length;
    public bool IsPractice(int round) => round < PracticeRounds;
    public int BlockAt(int round) => Math.Clamp((round - PracticeRounds) / RoundsPerBlock, 0, BlockValues.Length - 1);
    public int ValueAt(int round) => BlockValues[BlockAt(round)];

    public static AssessmentBlockProtocol? Parse(JsonElement config, bool visualExpansion = false)
    {
        if (!config.TryGetProperty("assessmentProtocol", out var element)) return null;
        var plan = JsonSerializer.Deserialize<AssessmentBlockProtocol>(element.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (plan is null || plan.Version != 2 || plan.PracticeRounds != 3
            || plan.RoundsPerBlock is not (6 or 8 or 10) || plan.BlockValues is not { Length: 3 }
            || plan.BlockValues.Any(value => value < (visualExpansion ? 2 : 50) || value > (visualExpansion ? 60 : 5000))
            || (visualExpansion ? plan.BlockValues[0] >= plan.BlockValues[1] || plan.BlockValues[1] >= plan.BlockValues[2]
                : plan.BlockValues[0] <= plan.BlockValues[1] || plan.BlockValues[1] <= plan.BlockValues[2]))
            throw new ArgumentException("Seviye tespit blok ayarları geçerli değil.");
        return plan;
    }
}

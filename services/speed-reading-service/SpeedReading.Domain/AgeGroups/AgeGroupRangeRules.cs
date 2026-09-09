namespace SpeedReading.Domain.AgeGroups;

public static class AgeGroupRangeRules
{
    public static bool Overlaps(
        int firstMinAge,
        int? firstMaxAge,
        int secondMinAge,
        int? secondMaxAge)
    {
        var firstMax = firstMaxAge ?? int.MaxValue;
        var secondMax = secondMaxAge ?? int.MaxValue;
        return firstMinAge <= secondMax && secondMinAge <= firstMax;
    }
}

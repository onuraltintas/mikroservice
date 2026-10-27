namespace Coaching.Domain.Entities;

/// <summary>
/// The learning profile stores the YKS language field as <c>DIL</c>; university programs and net targets name the same
/// exam <c>YDT</c> (<c>YDT_EN</c> for English nets). Every comparison between the two goes through here.
/// </summary>
public static class YksScoreTypes
{
    public static string? ProgramScoreType(string? profileScoreType) => profileScoreType is "DIL" ? "YDT" : profileScoreType;

    /// <summary>The field-specific net exam next to TYT for a profile or program score type.</summary>
    public static string AdvancedNetExam(string? scoreType) => scoreType switch
    {
        "SAY" => "AYT_SAY",
        "EA" => "AYT_EA",
        "SOZ" => "AYT_SOZ",
        "DIL" or "YDT" => "YDT_EN",
        _ => "TYT"
    };

    /// <summary>A program fits the profile when it is a TYT program or of the profile's own field.</summary>
    public static bool ProgramFitsProfile(string? profileScoreType, string? programScoreType) =>
        profileScoreType is null || programScoreType == "TYT" || programScoreType == ProgramScoreType(profileScoreType);
}

namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Single Tachistoscope words when the vocabulary has none. Two-word phrases come from <see cref="TachistoscopePhrases"/>.
/// </summary>
public static class TachistoscopeContent
{
    /// <summary>
    /// Used only when the vocabulary has no usable word: everyday words of 2–10 letters, none of them in
    /// <see cref="TachistoscopeWordExclusions"/> (difficulty comes from display time and length, not vocabulary).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultWords =
    [
        "su", "ev", "el", "ay", "göz", "yol", "kuş", "süt", "masa", "kapı", "okul", "elma", "kedi",
        "kitap", "kalem", "deniz", "ekmek", "bulut", "çiçek", "yağmur", "defter", "mutfak", "kardeş",
        "pencere", "kelebek", "arkadaş", "domates", "sandalye", "portakal", "öğretmen", "bisiklet",
        "battaniye", "buzdolabı", "kütüphane", "salatalık", "televizyon", "bilgisayar", "helikopter", "kaplumbağa"
    ];
}

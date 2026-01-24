namespace Coaching.Domain.Enums;

/// <summary>
/// Sınav tipi
/// </summary>
public enum ExamType
{
    Mock = 1,           // Deneme sınavı
    Weekly = 2,         // Haftalık test
    Monthly = 3,        // Aylık değerlendirme
    LGS = 4,            // Liseye Geçiş Sınavı
    YKS = 5,            // Yükseköğretim Kurumları Sınavı
    MidTerm = 6,        // Ara sınav
    Final = 7,          // Final
    Quiz = 8            // Kısa sınav
}

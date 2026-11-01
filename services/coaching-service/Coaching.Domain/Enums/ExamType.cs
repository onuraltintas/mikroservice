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
    Quiz = 8,           // Kısa sınav
    AGS = 9,            // KM1: MEB Akademi Giriş Sınavı ve ÖABT
    YDS = 10,           // KM1: Yabancı Dil Bilgisi Seviye Tespit Sınavı
    YOKDIL = 11         // KM1: Yükseköğretim Kurumları Yabancı Dil Sınavı
}

namespace Coaching.Domain.Enums;

/// <summary>
/// Koçluk seans tipi
/// </summary>
public enum SessionType
{
    OneOnOne = 1,    // 1-1 koçluk
    Group = 2        // Grup koçluğu
}

/// <summary>
/// Seans durumu
/// </summary>
public enum SessionStatus
{
    Scheduled = 1,   // Planlandı
    Completed = 2,   // Tamamlandı
    Cancelled = 3    // İptal edildi
}

/// <summary>
/// Katılım durumu
/// </summary>
public enum AttendanceStatus
{
    Present = 1,     // Katıldı
    Absent = 2,      // Katılmadı
    Late = 3,       // Geç katıldı
    Excused = 4      // Mazeret ile katılmadı
}

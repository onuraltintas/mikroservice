namespace Coaching.Domain.Enums;

/// <summary>
/// Ödev durumu
/// </summary>
public enum AssignmentStatus
{
    Active = 1,      // Aktif (devam ediyor)
    Completed = 2,   // Tamamlandı
    Cancelled = 3    // İptal edildi
}

/// <summary>
/// Öğrenci ödev durumu
/// </summary>
public enum StudentAssignmentStatus
{
    Assigned = 1,      // Atandı (henüz başlanmadı)
    InProgress = 2,    // Üzerinde çalışılıyor
    Submitted = 3,     // Teslim edildi
    Graded = 4         // Notlandırıldı
}

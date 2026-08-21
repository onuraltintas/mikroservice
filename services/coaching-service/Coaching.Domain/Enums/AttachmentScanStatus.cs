namespace Coaching.Domain.Enums;

/// <summary>
/// Fotoğraf/ek dosyanın yükleme ve güvenlik taraması durumu.
/// </summary>
public enum AttachmentScanStatus
{
    PendingUpload = 1,
    PendingScan = 2,
    Clean = 3,
    Rejected = 4
}

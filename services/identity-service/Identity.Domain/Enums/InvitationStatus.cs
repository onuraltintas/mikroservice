namespace Identity.Domain.Enums;

public enum InvitationStatus
{
    /// <summary>
    /// Davet beklemede, henüz kabul/red edilmedi
    /// </summary>
    Pending = 1,
    
    /// <summary>
    /// Davet kabul edildi
    /// </summary>
    Accepted = 2,
    
    /// <summary>
    /// Davet reddedildi
    /// </summary>
    Rejected = 3,
    
    /// <summary>
    /// Davet süresi doldu
    /// </summary>
    Expired = 4
}

using System;

namespace Notification.Domain.Entities;

public class EmailTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Şablonu çağırmak için kullanacağımız benzersiz isim (Örn: "Auth_Welcome")
    public string TemplateName { get; set; } = string.Empty;
    
    // Modül/Kategori (Örn: "Auth", "Coaching", "Payment")
    public string Category { get; set; } = string.Empty;
    
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    
    // Basit bir değişken listesi de tutabiliriz ama string parsing ile yapacağız
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

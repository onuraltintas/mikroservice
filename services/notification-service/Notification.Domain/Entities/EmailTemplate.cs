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

    public static EmailTemplate Create(string templateName, string category, string subject, string body)
    {
        return new EmailTemplate
        {
            TemplateName = templateName,
            Category = category,
            Subject = subject,
            Body = body,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string category, string subject, string body, bool isActive)
    {
        Category = category;
        Subject = subject;
        Body = body;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}

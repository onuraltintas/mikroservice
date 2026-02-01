using EduPlatform.Shared.Kernel.Primitives;

namespace Notification.Domain.Entities;

public class SupportRequest : AggregateRoot
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsProcessed { get; private set; }
    public string? AdminNote { get; private set; }

    private SupportRequest() { }

    public SupportRequest(Guid id, string firstName, string lastName, string email, string subject, string message) : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Subject = subject;
        Message = message;
        IsProcessed = false;
        CreatedAt = DateTime.UtcNow;
    }

    public void Process(string? adminNote)
    {
        IsProcessed = true;
        AdminNote = adminNote;
        UpdatedAt = DateTime.UtcNow;
    }
}

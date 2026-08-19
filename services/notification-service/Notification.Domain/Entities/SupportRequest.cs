using EduPlatform.Shared.Kernel.Primitives;
using System.Security.Cryptography;
using System.Text;

namespace Notification.Domain.Entities;

public class SupportRequest : AggregateRoot
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? IdempotencyKey { get; private set; }
    public bool IsProcessed { get; private set; }
    public string? AdminNote { get; private set; }

    private SupportRequest() { }

    public SupportRequest(
        Guid id,
        string firstName,
        string lastName,
        string email,
        string subject,
        string message,
        string? idempotencyKey = null) : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Subject = subject;
        Message = message;
        IdempotencyKey = idempotencyKey;
        IsProcessed = false;
        CreatedAt = DateTime.UtcNow;
    }

    public void Process(string? adminNote)
    {
        IsProcessed = true;
        AdminNote = adminNote;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasSamePayload(
        string firstName,
        string lastName,
        string email,
        string subject,
        string message)
    {
        return string.Equals(
            CreatePayloadFingerprint(FirstName, LastName, Email, Subject, Message),
            CreatePayloadFingerprint(firstName, lastName, email, subject, message),
            StringComparison.Ordinal);
    }

    private static string CreatePayloadFingerprint(
        string firstName,
        string lastName,
        string email,
        string subject,
        string message)
    {
        var fields = new[]
        {
            NormalizeText(firstName),
            NormalizeText(lastName),
            NormalizeEmail(email),
            NormalizeText(subject),
            NormalizeText(message)
        };
        var canonicalPayload = string.Join(
            "|",
            fields.Select(field => $"{field.Length}:{field}"));

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    private static string NormalizeText(string value) => value.Trim();

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
}

using EduPlatform.Shared.Kernel.Results;
using Identity.Application.LegalPages;
using Identity.Application.Registration;
using MediatR;

namespace Identity.Application.Commands.RegisterCourseManagement;

/// <summary>
/// maxkampus.com registration (19.11.2026): "Kurumum var" (<c>institution</c>) or "Özel ders veriyorum" (<c>teacher</c>).
/// The phone is required; the workspace opens under <paramref name="WorkspaceName"/>. Someone who already has an
/// eduivme account (Koçluk, Hızlı Okuma) registers with that account's e-mail and password and joins with it.
/// </summary>
public sealed record RegisterCourseManagementCommand(
    string Kind,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Phone,
    string? WorkspaceName,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    RegistrationUtm? Utm = null,
    /// <summary>The six-digit code of an existing account that has two-step verification.</summary>
    string? MfaCode = null) : IRequest<Result<RegistrationOutcome>>;

/// <summary>The details maxkampus.com asks of a new account, with a password or with Google.</summary>
public sealed record CourseManagementSignup(string Kind, string Phone, string? WorkspaceName);

/// <summary>Rules every maxkampus.com registration shares (password, Google, existing account).</summary>
public static class CourseManagementRegistration
{
    public const string InstitutionKind = "institution";
    public const string TeacherKind = "teacher";
    public const string OwnerRole = "CourseInstitutionOwner";
    public const string TeacherRole = "CourseTeacher";
    public const int WorkspaceNameLength = 200;

    public static string? RoleFor(string? kind) => kind switch
    {
        InstitutionKind => OwnerRole,
        TeacherKind => TeacherRole,
        _ => null
    };

    /// <summary>
    /// A Turkish mobile or landline number in the forms people write it ("0532 123 45 67", "+90 212 …", "5321234567"),
    /// stored as +90XXXXXXXXXX; a foreign number with its + and 8 to 15 digits stays as written. Null when it is not a number.
    /// </summary>
    public static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Any(c => !(char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' or '+' or '.'))) return null;
        var plus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (plus && digits.StartsWith("90")) digits = digits[2..];
        else if (plus) return digits.Length is >= 8 and <= 15 ? "+" + digits : null;
        else if (digits.Length == 12 && digits.StartsWith("90")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
        // Turkish numbers: ten digits, area or mobile code starting 2–5 (landline 2xx–4xx, mobile 5xx).
        return digits.Length == 10 && digits[0] is >= '2' and <= '5' ? "+90" + digits : null;
    }

    /// <summary>The workspace's name: the institution's, or the teacher's own name when none is given.</summary>
    public static string? WorkspaceName(string kind, string? given, string firstName, string lastName)
    {
        var name = given?.Trim();
        if (string.IsNullOrEmpty(name) && kind == TeacherKind) name = $"{firstName.Trim()} {lastName.Trim()}".Trim();
        return string.IsNullOrEmpty(name) || name.Length > WorkspaceNameLength ? null : name;
    }

    /// <summary>A workspace id for one account's registration: the same account always asks for the same workspace.</summary>
    public static Guid WorkspaceIdFor(Guid userId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("maxkampus-registration-workspace:" + userId.ToString("N")));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes.AsSpan(0, 16));
    }
}

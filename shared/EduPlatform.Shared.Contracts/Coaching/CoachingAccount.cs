namespace EduPlatform.Shared.Contracts.Coaching;

/// <summary>Shared account data only; education and product relationships belong to Coaching.</summary>
public sealed record CoachingAccount(Guid UserId, string FirstName, string LastName, string Email,
    bool IsActive, bool HasCoachingAccess, IReadOnlyList<string> Roles, DateTime? LastLoginAt)
{
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool IsEligible(string role) => IsActive && HasCoachingAccess && HasRole(role);
    public bool IsSystemAdministrator => IsActive && HasRole("SystemAdmin");
}

public sealed record CoachingAccountRequest(IReadOnlyList<Guid> UserIds);
public sealed record CoachingAccountResponse(IReadOnlyList<CoachingAccount> Accounts);
public sealed record CoachingAccountEmailRequest(string Email);

public sealed record CoachingProfileProvisioningRequested(Guid UserId, string Role, DateTime RequestedAt)
{
    public string Product { get; init; } = "coaching";
    public int SchemaVersion { get; init; } = 1;
}

namespace EduPlatform.Shared.Contracts.Billing;

/// <summary>An institution a user administers; the billing service opens one account per institution (decision 14.11.2026).</summary>
public sealed record BillingInstitution(Guid Id, string Name);

public sealed record BillingInstitutionsResponse(IReadOnlyList<BillingInstitution> Institutions);

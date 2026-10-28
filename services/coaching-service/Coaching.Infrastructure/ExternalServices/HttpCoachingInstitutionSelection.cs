using Coaching.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Coaching.Infrastructure.ExternalServices;

/// <summary>
/// Roadmap O13: reads the branch an institution admin selected (header <c>X-Coaching-Institution</c>). Identity only
/// honours it when the admin manages that institution; otherwise the admin's first institution is used.
/// </summary>
public sealed class HttpCoachingInstitutionSelection(IHttpContextAccessor http) : ICoachingInstitutionSelection
{
    public const string HeaderName = "X-Coaching-Institution";

    public Guid? PreferredInstitutionId =>
        http.HttpContext?.Request.Headers[HeaderName].FirstOrDefault() is { } value && Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}

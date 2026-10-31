using Identity.Application.ParentLinks;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>
/// V1 (parent panel): service-to-service checks for a parent reading a child's product data (Coaching and Speed reading
/// ask on every parent read). Service-key only; the gateway does not route /api/internal.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/parents")]
public sealed class InternalParentsController(IParentLinkService links) : ControllerBase
{
    [HttpPost("authorize-read")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> AuthorizeRead([FromBody] ParentReadAuthorizationRequest? request, CancellationToken ct)
    {
        if (request is null || request.ParentUserId == Guid.Empty || request.StudentUserId == Guid.Empty
            || !TryParseProduct(request.Product, out var product))
            return BadRequest("Parent read scope is invalid.");
        var authorization = await links.AuthorizeReadAsync(request.ParentUserId, request.StudentUserId, product, ct);
        return authorization is null
            ? StatusCode(StatusCodes.Status403Forbidden)
            : Ok(new ParentReadAuthorizationResponse(authorization.RelationshipId, authorization.Relationship));
    }

    private static bool TryParseProduct(string? value, out PlatformProduct product)
    {
        product = value switch
        {
            "Coaching" => PlatformProduct.Coaching,
            "SpeedReading" => PlatformProduct.SpeedReading,
            _ => default
        };
        return product != default;
    }
}

/// <summary>Product is "Coaching" or "SpeedReading".</summary>
public sealed record ParentReadAuthorizationRequest(Guid ParentUserId, Guid StudentUserId, string? Product);

public sealed record ParentReadAuthorizationResponse(Guid RelationshipId, string Relationship);

using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.ParentLinks;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>
/// Self-service parent links (roadmap Y7). Routed under /api/users so the gateway's identity user route serves it.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/users/me")]
public sealed class ParentLinksController(IParentLinkService links, ICurrentUserService actor) : ControllerBase
{
    /// <summary>The student creates a one-time code to hand to a parent; it replaces any unused code.</summary>
    [HttpPost("parent-link-codes")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateCode(CancellationToken ct) => Respond(await links.CreateCodeAsync(UserId, ct));

    /// <summary>The parent asks to link with the student who gave the code; the student approves it.</summary>
    [HttpPost("parent-links")]
    [Authorize(Roles = "Parent")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> RequestLink([FromBody] ParentLinkRequest request, CancellationToken ct) =>
        Respond(await links.RequestAsync(UserId, request.Code, request.Relationship, ct));

    [HttpGet("parent-links")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> List(CancellationToken ct) => Respond(await links.ListAsync(UserId, ct));

    [HttpPost("parent-links/{id:guid}/approve")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Respond(await links.ApproveAsync(UserId, id, ct));

    [HttpPost("parent-links/{id:guid}/revoke")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct) => Respond(await links.RevokeAsync(UserId, id, ct));

    /// <summary>V1: the student chooses which products the parent sees (both start visible).</summary>
    [HttpPut("parent-links/{id:guid}/products")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> SetProducts(Guid id, [FromBody] ParentLinkProductsRequest request, CancellationToken ct)
    {
        if (request?.Coaching is not { } coaching || request.SpeedReading is not { } speedReading)
            return BadRequest(new { error = new Error("ParentLink.InvalidProducts", "Her ürün için görünürlük seçilmelidir.") });
        return Respond(await links.SetProductVisibilityAsync(UserId, id, coaching, speedReading, ct));
    }

    private Guid UserId => actor.UserId ?? Guid.Empty;

    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    private IActionResult Respond(Result result) => result.IsSuccess ? NoContent() : Failure(result.Error);
    private IActionResult Failure(Error error) => error.Code switch
    {
        "ParentLink.Forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { error }),
        "ParentLink.NotFound" => NotFound(new { error }),
        "ParentLink.AlreadyLinked" or "ParentLink.NotPending" => Conflict(new { error }),
        _ => BadRequest(new { error })
    };
}

public sealed record ParentLinkRequest(string Code, ParentRelationship Relationship);
public sealed record ParentLinkProductsRequest(bool? Coaching, bool? SpeedReading);

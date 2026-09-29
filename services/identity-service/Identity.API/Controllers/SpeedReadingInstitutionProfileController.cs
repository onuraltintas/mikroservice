using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/institution/speed-reading")]
[Authorize(Roles = "InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class SpeedReadingInstitutionProfileController(
    ICurrentUserService currentUser,
    IInstitutionRepository institutions) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentSpeedReadingInstitutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentSpeedReadingInstitutionResponse>> GetMe(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var institutionId = await institutions.GetInstitutionIdByAdminIdAsync(
            userId,
            PlatformProduct.SpeedReading,
            cancellationToken);
        if (institutionId is null || institutionId == Guid.Empty)
            return Forbid();

        var institution = await institutions.GetDtoByIdAsync(institutionId.Value, cancellationToken);
        if (institution is null || !institution.IsActive)
            return NotFound();

        return Ok(new CurrentSpeedReadingInstitutionResponse(institution.Id, institution.Name));
    }
}

public sealed record CurrentSpeedReadingInstitutionResponse(Guid InstitutionId, string InstitutionName);

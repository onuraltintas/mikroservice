using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduPlatform.Shared.Security.Authorization;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/[controller]")]
[HasPermission(Permissions.Operations.View)]
public class ConfigurationsController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationsController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConfigurationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var configs = await _configurationService.GetAllConfigurationsAsync(cancellationToken);
        return Ok(configs);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<string>> GetValue(string key, CancellationToken cancellationToken)
    {
        var value = await _configurationService.GetManageableConfigurationValueAsync(key, cancellationToken);
        if (value == null) return NotFound();
        return Ok(value);
    }

    /// <summary>
    /// Changes the MFA policy control plane itself. Making a category stricter stays
    /// available to a SystemAdmin without step-up, so MFA can be switched on before
    /// anyone has enrolled. Making it weaker needs a session verified with MFA: a
    /// phished password alone must not be able to switch the second factor off.
    /// </summary>
    [HttpPut("mfa/{category}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> UpdateMfaPolicy(
        string category,
        UpdateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!MfaOperationCategories.IsKnown(category))
        {
            return BadRequest(new { message = "Geçersiz MFA kategorisi." });
        }

        if (!MfaPolicyModes.IsKnown(request.Value))
        {
            return BadRequest(new { message = "Geçersiz MFA politikası." });
        }

        var key = MfaOperationCategories.ConfigurationKey(category);
        var current = await _configurationService.GetManageableConfigurationValueAsync(key, cancellationToken);
        if (MfaPolicyChangeRules.Weakens(current, request.Value)
            && !MfaPolicyChangeRules.IsMfaVerified(User))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "Auth.MfaRequired",
                message = "MFA politikasını gevşetmek için iki adımlı doğrulamayla açılmış bir oturum gerekir."
            });
        }

        await _configurationService.UpdateConfigurationAsync(
            MfaOperationCategories.ConfigurationKey(category),
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("public/{key}")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> GetPublicValue(string key, CancellationToken cancellationToken)
    {
        var value = await _configurationService.GetPublicConfigurationValueAsync(key, cancellationToken);
        if (value == null) return NotFound();
        return Ok(value);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [MfaCategory(MfaOperationCategories.System)]
    public async Task<ActionResult<ConfigurationDto>> Create(CreateConfigurationRequest request, CancellationToken cancellationToken)
    {
        var config = await _configurationService.CreateConfigurationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetValue), new { key = config.Key }, config);
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [MfaCategory(MfaOperationCategories.System)]
    public async Task<IActionResult> Update(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken)
    {
        await _configurationService.UpdateConfigurationAsync(key, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{key}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [MfaCategory(MfaOperationCategories.System)]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        await _configurationService.DeleteConfigurationAsync(key, cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh-cache")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [MfaCategory(MfaOperationCategories.System)]
    public async Task<IActionResult> RefreshCache(CancellationToken cancellationToken)
    {
        await _configurationService.RefreshCacheAsync(cancellationToken);
        return Ok(new { message = "Cache refreshed successfully" });
    }
}

/// <summary>Strictness order of MFA policy modes: disabled &lt; mutations &lt; required.</summary>
public static class MfaPolicyChangeRules
{
    /// <summary>
    /// True when <paramref name="requested"/> is less strict than <paramref name="current"/>.
    /// A missing or unreadable current value counts as "required", the effective default.
    /// </summary>
    public static bool Weakens(string? current, string? requested) => Rank(requested) < Rank(current ?? MfaPolicyModes.Required);

    public static bool IsMfaVerified(System.Security.Claims.ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && user.Claims.Any(claim => claim.Type == "amr" && claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase));

    private static int Rank(string? mode) =>
        mode is null ? 2
        : mode.Equals(MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase) ? 0
        : mode.Equals(MfaPolicyModes.MutationsOnly, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;
}

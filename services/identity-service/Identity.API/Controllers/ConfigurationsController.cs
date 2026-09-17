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
    /// Changes the MFA policy control plane itself. This route deliberately
    /// remains available to a SystemAdmin without the policy it is changing,
    /// so an admin cannot be locked out before MFA has been enrolled.
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

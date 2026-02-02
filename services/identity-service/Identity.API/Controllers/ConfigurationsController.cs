using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
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
        var value = await _configurationService.GetConfigurationValueAsync(key, cancellationToken);
        if (value == null) return NotFound();
        return Ok(value);
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
    public async Task<ActionResult<ConfigurationDto>> Create(CreateConfigurationRequest request, CancellationToken cancellationToken)
    {
        var config = await _configurationService.CreateConfigurationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetValue), new { key = config.Key }, config);
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Update(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken)
    {
        await _configurationService.UpdateConfigurationAsync(key, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{key}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        await _configurationService.DeleteConfigurationAsync(key, cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh-cache")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> RefreshCache(CancellationToken cancellationToken)
    {
        await _configurationService.RefreshCacheAsync(cancellationToken);
        return Ok(new { message = "Cache refreshed successfully" });
    }
}

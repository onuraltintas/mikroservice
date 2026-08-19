using Identity.Application.DTOs.Logs;
using Identity.Application.Interfaces;
using Identity.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduPlatform.Shared.Security.Authorization;

namespace Identity.API.Controllers.Settings;

[ApiController]
[ApiVersion(1.0)]
[Route("api/system-logs")]
[HasPermission(Permissions.Operations.View)]
public class SystemLogsController : ControllerBase
{
    private readonly ISystemLogService _systemLogService;
    public SystemLogsController(ISystemLogService systemLogService, ILogger<SystemLogsController> logger)
    {
        _systemLogService = systemLogService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedLogsResponse>> GetLogs([FromQuery] LogFilterRequest request, CancellationToken cancellationToken)
    {
        var logs = await _systemLogService.GetLogsAsync(request, cancellationToken);
        return Ok(logs);
    }

    [HttpGet("applications")]
    public async Task<ActionResult<List<string>>> GetApplications(CancellationToken cancellationToken)
    {
        var applications = await _systemLogService.GetApplicationsAsync(cancellationToken);
        return Ok(applications);
    }
    [HttpGet("retention-policies")]
    public async Task<ActionResult<List<RetentionPolicyDto>>> GetRetentionPolicies(CancellationToken cancellationToken)
    {
        var policies = await _systemLogService.GetRetentionPoliciesAsync(cancellationToken);
        return Ok(policies);
    }

    [HttpPost("retention-policies")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    public async Task<ActionResult<RetentionPolicyDto>> CreateRetentionPolicy([FromBody] CreateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await _systemLogService.CreateRetentionPolicyAsync(request, cancellationToken);
        if (policy == null) return BadRequest("Failed to create policy");
        return Ok(policy);
    }

    [HttpDelete("retention-policies/{id}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    public async Task<IActionResult> DeleteRetentionPolicy(string id, CancellationToken cancellationToken)
    {
        var result = await _systemLogService.DeleteRetentionPolicyAsync(id, cancellationToken);
        if (!result) return BadRequest("Failed to delete policy");
        return Ok();
    }

    [HttpGet("seq-url")]
    public ActionResult<string> GetSeqUrl()
    {
        return Ok(new { Url = _systemLogService.GetSeqUrl() });
    }
}

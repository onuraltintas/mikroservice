using Identity.Application.DTOs.Logs;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers.Settings;

[ApiController]
[Route("api/system-logs")]
[Authorize(Roles = "Admin, SystemAdmin")] // Restrict to admins
public class SystemLogsController : ControllerBase
{
    private readonly ISystemLogService _systemLogService;
    private readonly ILogger<SystemLogsController> _logger;

    public SystemLogsController(ISystemLogService systemLogService, ILogger<SystemLogsController> logger)
    {
        _systemLogService = systemLogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedLogsResponse>> GetLogs([FromQuery] LogFilterRequest request, CancellationToken cancellationToken)
    {
        try 
        {
            var logs = await _systemLogService.GetLogsAsync(request, cancellationToken);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch system logs");
            return StatusCode(500, $"Error fetching logs: {ex.Message}");
        }
    }

    [HttpGet("applications")]
    public async Task<ActionResult<List<string>>> GetApplications(CancellationToken cancellationToken)
    {
        try 
        {
            var applications = await _systemLogService.GetApplicationsAsync(cancellationToken);
            return Ok(applications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch applications");
            return Ok(new List<string>()); // Return empty list on error
        }
    }
    [HttpGet("retention-policies")]
    public async Task<ActionResult<List<RetentionPolicyDto>>> GetRetentionPolicies(CancellationToken cancellationToken)
    {
        try
        {
            var policies = await _systemLogService.GetRetentionPoliciesAsync(cancellationToken);
            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch retention policies");
            return StatusCode(500, $"Error fetching policies: {ex.Message}");
        }
    }

    [HttpPost("retention-policies")]
    public async Task<ActionResult<RetentionPolicyDto>> CreateRetentionPolicy([FromBody] CreateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _systemLogService.CreateRetentionPolicyAsync(request, cancellationToken);
            if (policy == null) return BadRequest("Failed to create policy");
            return Ok(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create retention policy");
            return StatusCode(500, $"Error creating policy: {ex.Message}");
        }
    }

    [HttpDelete("retention-policies/{id}")]
    public async Task<IActionResult> DeleteRetentionPolicy(string id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _systemLogService.DeleteRetentionPolicyAsync(id, cancellationToken);
            if (!result) return BadRequest("Failed to delete policy");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete retention policy");
            return StatusCode(500, $"Error deleting policy: {ex.Message}");
        }
    }

    [HttpGet("seq-url")]
    public ActionResult<string> GetSeqUrl()
    {
        return Ok(new { Url = "http://localhost:5341" }); // Client side should reach localhost:5341
    }
}

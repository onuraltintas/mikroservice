using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.AgeGroups;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/age-group-configurations")]
public sealed class AgeGroupConfigurationsController(ISpeedReadingAgeGroups ageGroups) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        Ok(await ageGroups.GetAllAsync(activeOnly, cancellationToken));

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken = default) =>
        Ok(await ageGroups.GetAllAsync(true, cancellationToken));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await ageGroups.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("by-age/{age:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByAge(int age, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ageGroups.GetByAgeAsync(age, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("recommendations/{age:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecommendations(int age, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ageGroups.GetRecommendationsAsync(age, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAgeGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await ageGroups.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAgeGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            return await ageGroups.UpdateAsync(id, userId, request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await ageGroups.DeleteAsync(id, userId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}

using System.Text;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.BulkUsers;
using Identity.Application.Commands.CreateUser;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using Identity.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/users/bulk")]
public sealed class BulkUsersController : ControllerBase
{
    internal const long MaxImportBytes = 5 * 1024 * 1024;
    internal const int MaxImportRows = 1_000;
    internal const int MaxRoleAssignmentUsers = 100;
    internal const int MaxExportRows = 50_000;
    private const int ExportPageSize = 100;

    private readonly IMediator _mediator;
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BulkUsersController> _logger;

    public BulkUsersController(
        IMediator mediator,
        IIdentityService identityService,
        IUserRepository userRepository,
        ILogger<BulkUsersController> logger)
    {
        _mediator = mediator;
        _identityService = identityService;
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet("template")]
    [HasPermission(Permissions.Users.View)]
    [Authorize(Roles = "SystemAdmin")]
    [Produces("text/csv")]
    public IActionResult DownloadTemplate()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(IdentityBulkUserCsv.Header + Environment.NewLine))
            .ToArray();
        return File(bytes, "text/csv; charset=utf-8", "users-import-template.csv");
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    [HasPermission(Permissions.Users.Create)]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [ProducesResponseType(typeof(BulkUserOperationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { Error = new { Code = "BulkUsers.FileRequired", Description = "CSV dosyası gereklidir." } });

        if (file.Length > MaxImportBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Error = new { Code = "BulkUsers.InvalidFile", Description = "Yalnızca en fazla 5 MB boyutunda CSV dosyası yüklenebilir." } });
        }

        string content;
        try
        {
            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return BadRequest(new { Error = new { Code = "BulkUsers.InvalidEncoding", Description = "CSV dosyası UTF-8 olarak kodlanmalıdır." } });
        }

        var parsed = IdentityBulkUserCsv.Parse(content, MaxImportRows);
        if (parsed.Errors.Any(error => !error.StartsWith("Satır ", StringComparison.Ordinal)))
        {
            return BadRequest(new { Error = new { Code = "BulkUsers.InvalidCsv", Description = parsed.Errors[0] } });
        }

        var errors = parsed.Errors.ToList();
        var succeeded = 0;
        foreach (var row in parsed.Rows)
        {
            try
            {
                var result = await _mediator.Send(
                    new CreateUserCommand(
                        row.FirstName,
                        row.LastName,
                        row.Email,
                        row.PhoneNumber,
                        row.Role),
                    cancellationToken);
                if (result.IsSuccess)
                {
                    succeeded++;
                }
                else
                {
                    errors.Add($"Satır {row.RowNumber}: {result.Error.Description}");
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bulk user import failed for row {RowNumber}.", row.RowNumber);
                errors.Add($"Satır {row.RowNumber}: Kullanıcı işlenemedi.");
            }
        }

        return Ok(new BulkUserOperationResult(succeeded, errors.Count, errors));
    }

    [HttpGet("export")]
    [HasPermission(Permissions.Users.View)]
    [Authorize(Roles = "SystemAdmin")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var users = new List<Identity.Application.Queries.GetUserProfile.UserProfileDto>();
        var pageNumber = 1;
        var truncated = false;
        var totalCount = 0;

        while (users.Count < MaxExportRows)
        {
            var result = await _mediator.Send(
                new GetAllUsersQuery(pageNumber, ExportPageSize, search, role, isActive),
                cancellationToken);
            if (result.IsFailure)
                return BadRequest(new { Error = result.Error });

            totalCount = result.Value.TotalCount;
            users.AddRange(result.Value.Items);
            if (result.Value.Items.Count < ExportPageSize || users.Count >= result.Value.TotalCount)
                break;

            pageNumber++;
        }

        if (users.Count > MaxExportRows)
        {
            users = users.Take(MaxExportRows).ToList();
        }

        truncated = totalCount > users.Count;

        Response.Headers.Append("X-Export-Truncated", truncated ? "true" : "false");
        var csv = IdentityBulkUserCsv.Export(users.Select(IdentityBulkUserCsv.ToExportRow));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"users-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpPost("role")]
    [HasPermission(Permissions.Users.Edit)]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [ProducesResponseType(typeof(BulkUserOperationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRole(
        [FromBody] BulkRoleAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var userIds = request.UserIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];
        var roleName = request.RoleName?.Trim();

        if (userIds.Length == 0 || userIds.Length > MaxRoleAssignmentUsers || string.IsNullOrWhiteSpace(roleName))
        {
            return BadRequest(new { Error = new { Code = "BulkUsers.InvalidRoleRequest", Description = $"1-{MaxRoleAssignmentUsers} kullanıcı ve geçerli bir rol seçilmelidir." } });
        }

        var availableRoles = await _identityService.GetAvailableRolesAsync(cancellationToken);
        var canonicalRole = availableRoles.IsSuccess
            ? availableRoles.Value.FirstOrDefault(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase))
            : null;
        if (canonicalRole is null)
        {
            return BadRequest(new { Error = new { Code = "BulkUsers.RoleNotFound", Description = "Seçilen rol bulunamadı veya artık aktif değil." } });
        }
        roleName = canonicalRole;

        var errors = new List<string>();
        var succeeded = 0;
        foreach (var userId in userIds)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                {
                    errors.Add($"{userId}: Kullanıcı bulunamadı.");
                    continue;
                }

                var existingRoles = user.Roles
                    .Where(userRole => userRole.Role is not null)
                    .Select(userRole => userRole.Role.Name)
                    .ToArray();
                if (request.RemoveExistingRoles)
                {
                    foreach (var existingRole in existingRoles.Where(existingRole =>
                                 !string.Equals(existingRole, roleName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var removeResult = await _identityService.RemoveRoleAsync(userId, existingRole, cancellationToken);
                        if (removeResult.IsFailure)
                        {
                            errors.Add($"{userId}: {removeResult.Error.Description}");
                            goto NextUser;
                        }
                    }
                }

                var assignResult = await _identityService.AssignRoleAsync(userId, roleName, cancellationToken);
                if (assignResult.IsFailure)
                {
                    errors.Add($"{userId}: {assignResult.Error.Description}");
                    goto NextUser;
                }

                succeeded++;

            NextUser:
                ;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bulk role assignment failed for user {UserId}.", userId);
                errors.Add($"{userId}: Kullanıcı rolü güncellenemedi.");
            }
        }

        return Ok(new BulkUserOperationResult(succeeded, errors.Count, errors));
    }

    public sealed record BulkRoleAssignmentRequest(
        IReadOnlyList<Guid> UserIds,
        string RoleName,
        bool RemoveExistingRoles = false);

    public sealed record BulkUserOperationResult(
        int Succeeded,
        int Failed,
        IReadOnlyList<string> Errors);
}

using Asp.Versioning;
using Coaching.Application.Attachments;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>Roadmap O3: teacher–student and teacher–parent messages.</summary>
[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Teacher,Parent")]
[Route("api/coaching/messages")]
public sealed class CoachingMessagesController(CoachingMessagingService messages, ICurrentUserService actor) : ControllerBase
{
    private const long UploadLimit = AssignmentAttachmentPolicy.MaxFileSizeBytes + 65_536;

    [HttpGet("contacts")]
    public async Task<IActionResult> Contacts(CancellationToken ct) => Ok(await messages.ContactsAsync(UserId, ct));

    [HttpGet("conversations")]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await messages.ListAsync(UserId, pageNumber, pageSize, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) => Ok(new { count = await messages.UnreadCountAsync(UserId, ct) });

    [HttpPost("conversations")]
    [RequestSizeLimit(1_024), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Start([FromBody] CoachingConversationStart request, CancellationToken ct) =>
        Ok(await messages.StartAsync(UserId, request, ct));

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, [FromQuery] DateTime? before, CancellationToken ct) =>
        Ok(await messages.GetAsync(UserId, conversationId, before?.ToUniversalTime(), ct));

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [RequestSizeLimit(32_768), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Send(Guid conversationId, [FromBody] CoachingMessageSend request, CancellationToken ct) =>
        Closed(async () => Ok(await messages.SendAsync(UserId, conversationId, request, ct)));

    /// <summary>The raw file is the request body; its name comes in the query. It must be sent with a message within a day.</summary>
    [HttpPost("conversations/{conversationId:guid}/attachments")]
    [RequestSizeLimit(UploadLimit), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Upload(Guid conversationId, [FromQuery] string fileName, CancellationToken ct) => Closed(async () =>
    {
        var contentType = (Request.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream")
            contentType = AssignmentAttachmentPolicy.ContentTypeForFileName(fileName ?? string.Empty);
        return Ok(await messages.UploadAsync(UserId, conversationId, fileName ?? string.Empty, contentType, Request.Body, ct));
    });

    [HttpGet("conversations/{conversationId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        var file = await messages.OpenAttachmentAsync(UserId, conversationId, attachmentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");

    private static async Task<IActionResult> Closed(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException error) when (error.Code == "Messaging.Closed")
        { return new ConflictObjectResult(new { code = error.Code, message = error.Message }); }
    }
}

/// <summary>Roadmap O3: the institution lists its teachers' conversations and reviews one with a recorded reason.</summary>
[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[MfaCategory(MfaOperationCategories.Institutions)]
[Route("api/coaching/institution/messages")]
[Route("api/coaching/institutions/{institutionId:guid}/messages")]
public sealed class CoachingInstitutionMessagesController(CoachingMessagingService messages, CoachingProfileStore profiles,
    ICoachingAccountDirectory accounts, ICurrentUserService actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? institutionId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await messages.ListInstitutionAsync(UserId, await Resolve(institutionId, ct), pageNumber, pageSize, ct));

    [HttpPost("{conversationId:guid}/review")]
    [RequestSizeLimit(4_096), EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Review(Guid? institutionId, Guid conversationId, [FromBody] CoachingConversationReviewRequest request, CancellationToken ct) =>
        Ok(await messages.ReviewAsync(UserId, await Resolve(institutionId, ct), conversationId, request.Reason, ct));

    [HttpGet("{conversationId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid? institutionId, Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        await Resolve(institutionId, ct);
        var file = await messages.OpenAttachmentAsync(UserId, conversationId, attachmentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    private Guid UserId => actor.UserId ?? throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
    private async Task<Guid> Resolve(Guid? institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(UserId, ct);
        if (scope is null) throw new BusinessRuleException("Authorization.Forbidden", "Kurum yetkisi gereklidir.");
        var id = institutionId ?? scope.InstitutionId ?? throw new ArgumentException("Kurum seçin.");
        return await profiles.RequireInstitutionAdminAsync(UserId, id, ct);
    }
}

public sealed record CoachingConversationReviewRequest(string? Reason);

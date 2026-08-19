using EduPlatform.Shared.Kernel.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.DTOs;
using Notification.Application.Interfaces;

namespace Notification.Application.Queries;

public sealed record GetSupportRequestsQuery(
    int PageNumber = 1,
    int PageSize = 25,
    bool? IsProcessed = null,
    string? Search = null) : IRequest<Result<PagedSupportRequestsDto>>;

public sealed class GetSupportRequestsQueryValidator : AbstractValidator<GetSupportRequestsQuery>
{
    public GetSupportRequestsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(200).When(query => query.Search is not null);
    }
}

public sealed class GetSupportRequestsQueryHandler
    : IRequestHandler<GetSupportRequestsQuery, Result<PagedSupportRequestsDto>>
{
    private readonly INotificationDbContext _context;

    public GetSupportRequestsQueryHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedSupportRequestsDto>> Handle(
        GetSupportRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.SupportRequests.AsNoTracking().AsQueryable();
        if (request.IsProcessed.HasValue)
        {
            query = query.Where(item => item.IsProcessed == request.IsProcessed.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var pattern = $"%{search.ToLowerInvariant()}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Email.ToLower(), pattern)
                || EF.Functions.Like(item.Subject.ToLower(), pattern)
                || EF.Functions.Like(item.Message.ToLower(), pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new SupportRequestDto(
                item.Id,
                item.FirstName,
                item.LastName,
                item.Email,
                item.Subject,
                item.Message,
                item.IdempotencyKey,
                item.IsProcessed,
                item.AdminNote,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedSupportRequestsDto(
            items,
            totalCount,
            request.PageNumber,
            request.PageSize));
    }
}

public sealed record GetSupportRequestQuery(Guid Id) : IRequest<Result<SupportRequestDto>>;

public sealed class GetSupportRequestQueryHandler
    : IRequestHandler<GetSupportRequestQuery, Result<SupportRequestDto>>
{
    private readonly INotificationDbContext _context;

    public GetSupportRequestQueryHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SupportRequestDto>> Handle(
        GetSupportRequestQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _context.SupportRequests.AsNoTracking()
            .Where(support => support.Id == request.Id)
            .Select(support => new SupportRequestDto(
                support.Id,
                support.FirstName,
                support.LastName,
                support.Email,
                support.Subject,
                support.Message,
                support.IdempotencyKey,
                support.IsProcessed,
                support.AdminNote,
                support.CreatedAt,
                support.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? Result.Failure<SupportRequestDto>(new Error("SupportRequest.NotFound", "Destek talebi bulunamadı."))
            : Result.Success(item);
    }
}

public sealed record GetEmailTemplatesQuery : IRequest<Result<IReadOnlyList<Notification.Application.DTOs.EmailTemplateDto>>>;

public sealed class GetEmailTemplatesQueryHandler
    : IRequestHandler<GetEmailTemplatesQuery, Result<IReadOnlyList<Notification.Application.DTOs.EmailTemplateDto>>>
{
    private readonly INotificationDbContext _context;

    public GetEmailTemplatesQueryHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<Notification.Application.DTOs.EmailTemplateDto>>> Handle(
        GetEmailTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await _context.EmailTemplates
            .AsNoTracking()
            .OrderBy(template => template.Category)
            .ThenBy(template => template.TemplateName)
            .Select(template => new Notification.Application.DTOs.EmailTemplateDto(
                template.Id,
                template.TemplateName,
                template.Category,
                template.Subject,
                template.Body,
                template.IsActive,
                template.CreatedAt,
                template.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<Notification.Application.DTOs.EmailTemplateDto>>(templates);
    }
}

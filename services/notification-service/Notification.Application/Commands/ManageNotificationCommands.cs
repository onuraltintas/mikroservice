using EduPlatform.Shared.Kernel.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Application.Commands.ManageNotifications;

public sealed record ProcessSupportRequestCommand(Guid Id, string? AdminNote) : IRequest<Result>;

public sealed class ProcessSupportRequestCommandValidator : AbstractValidator<ProcessSupportRequestCommand>
{
    public ProcessSupportRequestCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.AdminNote).MaximumLength(2_000)
            .When(command => command.AdminNote is not null);
    }
}

public sealed class ProcessSupportRequestCommandHandler
    : IRequestHandler<ProcessSupportRequestCommand, Result>
{
    private readonly INotificationDbContext _context;

    public ProcessSupportRequestCommandHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ProcessSupportRequestCommand request, CancellationToken cancellationToken)
    {
        var supportRequest = await _context.SupportRequests
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (supportRequest is null)
        {
            return Result.Failure(new Error("SupportRequest.NotFound", "Destek talebi bulunamadı."));
        }

        supportRequest.Process(request.AdminNote?.Trim());
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record CreateEmailTemplateCommand(
    string TemplateName,
    string Category,
    string Subject,
    string Body) : IRequest<Result<Guid>>;

public sealed class CreateEmailTemplateCommandValidator : AbstractValidator<CreateEmailTemplateCommand>
{
    public CreateEmailTemplateCommandValidator()
    {
        RuleFor(command => command.TemplateName).NotEmpty().MaximumLength(100)
            .Matches("^[A-Za-z0-9._-]+$");
        RuleFor(command => command.Category).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(998);
        RuleFor(command => command.Body).NotEmpty().MaximumLength(100_000);
    }
}

public sealed record UpdateEmailTemplateCommand(
    Guid Id,
    string Category,
    string Subject,
    string Body,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Category).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(998);
        RuleFor(command => command.Body).NotEmpty().MaximumLength(100_000);
    }
}

public sealed class CreateEmailTemplateCommandHandler
    : IRequestHandler<CreateEmailTemplateCommand, Result<Guid>>
{
    private readonly INotificationDbContext _context;

    public CreateEmailTemplateCommandHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var name = request.TemplateName.Trim();
        if (await _context.EmailTemplates.AnyAsync(item => item.TemplateName == name, cancellationToken))
        {
            return Result.Failure<Guid>(new Error("EmailTemplate.Exists", "Bu şablon adı zaten kullanılıyor."));
        }

        var template = EmailTemplate.Create(name, request.Category.Trim(), request.Subject.Trim(), request.Body);
        _context.EmailTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(template.Id);
    }
}

public sealed class UpdateEmailTemplateCommandHandler
    : IRequestHandler<UpdateEmailTemplateCommand, Result>
{
    private readonly INotificationDbContext _context;

    public UpdateEmailTemplateCommandHandler(INotificationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (template is null)
        {
            return Result.Failure(new Error("EmailTemplate.NotFound", "E-posta şablonu bulunamadı."));
        }

        template.Update(request.Category.Trim(), request.Subject.Trim(), request.Body, request.IsActive);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

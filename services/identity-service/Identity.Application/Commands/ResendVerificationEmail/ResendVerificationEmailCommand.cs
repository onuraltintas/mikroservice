using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;
using Identity.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.ResendVerificationEmail;

/// <param name="Product">PR #111 second review H1: the site asking ("coaching" / "speed-reading"), so the e-mail and
/// its link are that product's. Used only when the account has that product; otherwise the account's single
/// Coaching/Speed-reading product decides, and with both or neither the role decides as before.</param>
public record ResendVerificationEmailCommand(string Email, string? Product = null) : IRequest<Result>;

public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ResendVerificationEmailCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork, IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (user.EmailConfirmed)
            return Result.Failure(new Error("User.EmailAlreadyConfirmed", "E-posta adresi zaten doğrulanmış."));

        // Generate new token
        user.GenerateEmailVerificationToken();
        var primaryRole = user.Roles
            .OrderBy(role => role.Role.Name)
            .Select(role => role.Role.Name)
            .FirstOrDefault() ?? Identity.Domain.Enums.UserRole.Student.ToString();

        // Publish Event
        await _publishEndpoint.Publish(new UserRegisteredEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.EmailVerificationToken ?? "",
            primaryRole
        ) { Product = ResolveProduct(user, request.Product) }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string? ResolveProduct(Identity.Domain.Entities.User user, string? requested)
    {
        if (PlatformProductExtensions.TryParseRouteValue(requested, out var product)
            && product is PlatformProduct.Coaching or PlatformProduct.SpeedReading
            && user.HasProductAccess(product))
            return product.ToRouteValue();
        var products = new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading }.Where(user.HasProductAccess).ToArray();
        return products.Length == 1 ? products[0].ToRouteValue() : null;
    }
}

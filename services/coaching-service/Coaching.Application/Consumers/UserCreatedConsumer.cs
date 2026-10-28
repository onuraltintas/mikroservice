using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Logging;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedConsumer> _logger;
    private readonly IAcademicGoalRepository _goalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserCreatedConsumer(
        ILogger<UserCreatedConsumer> logger,
        IAcademicGoalRepository goalRepository,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _goalRepository = goalRepository;
        _unitOfWork = unitOfWork;
    }

    public Task Consume(ConsumeContext<UserCreatedEvent> context) => HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(UserCreatedEvent message, CancellationToken cancellationToken)
    {
        if (message.Product is not null && !string.Equals(message.Product, "coaching", StringComparison.OrdinalIgnoreCase))
            return;
        if (!message.Role.Equals("Student", StringComparison.OrdinalIgnoreCase))
            return;
        // Idempotent: redelivery or the e-mail confirmation event never adds a second welcome goal.
        if (await WelcomeGoal.EnsureAsync(_goalRepository, _unitOfWork, message.UserId, cancellationToken))
            _logger.LogInformation("Welcome goal created for Student: {UserId}", message.UserId);
    }
}

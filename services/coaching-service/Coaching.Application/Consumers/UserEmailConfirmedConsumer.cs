using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Logging;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.Consumers;

public class UserEmailConfirmedConsumer : IConsumer<UserEmailConfirmedEvent>
{
    private readonly ILogger<UserEmailConfirmedConsumer> _logger;
    private readonly IAcademicGoalRepository _goalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserEmailConfirmedConsumer(
        ILogger<UserEmailConfirmedConsumer> logger,
        IAcademicGoalRepository goalRepository,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _goalRepository = goalRepository;
        _unitOfWork = unitOfWork;
    }

    public Task Consume(ConsumeContext<UserEmailConfirmedEvent> context) => HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(UserEmailConfirmedEvent message, CancellationToken cancellationToken)
    {
        // The event carries no product. Only students Coaching already knows get the Coaching welcome goal;
        // Speed Reading-only students are not Coaching data subjects. No e-mail address in logs.
        if (!message.Role.Equals("Student", StringComparison.OrdinalIgnoreCase)
            || !await _goalRepository.IsCoachingStudentAsync(message.UserId, cancellationToken))
            return;
        if (await WelcomeGoal.EnsureAsync(_goalRepository, _unitOfWork, message.UserId, cancellationToken))
            _logger.LogInformation("Welcome goal created for Student: {UserId}", message.UserId);
    }
}

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

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("UserCreatedEvent received for User: {Email} ({Role})", message.Email, message.Role);

        // Eğer yeni kullanıcı bir Öğrenci ise, ona otomatik bir "Hoşgeldin Hedefi" ata.
        if (message.Role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var welcomeGoal = AcademicGoal.Create(
                    studentId: message.UserId,
                    title: "Profilini Tamamla ve İlk Koçunu Bul",
                    category: GoalCategory.StudyHabits,
                    setByTeacherId: null
                );

                welcomeGoal.UpdateDetails(description: "Platforma hoşgeldin! İlk adım olarak profil bilgilerini doldur ve kendine uygun bir koç aramaya başla.");
                
                // Hedef tarihi 7 gün sonrası
                welcomeGoal.SetTarget(targetDate: DateTime.UtcNow.AddDays(7), targetScore: null);

                await _goalRepository.AddAsync(welcomeGoal, context.CancellationToken);
                await _unitOfWork.SaveChangesAsync(context.CancellationToken);

                _logger.LogInformation("Welcome goal created for Student: {UserId}", message.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create welcome goal for Student: {UserId}", message.UserId);
                // Hata durumunda mesajı tekrar kuyruğa atmak veya Dead Letter Queue'ya yollamak MassTransit tarafından yönetilir.
                throw; 
            }
        }
    }
}

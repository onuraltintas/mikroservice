using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistence;
using Notification.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Notification.API.Hubs;
using Microsoft.EntityFrameworkCore;

namespace Notification.API.Services;

public class NotificationManager : INotificationService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationManager(NotificationDbContext dbContext, IHubContext<NotificationHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, string type, string? relatedEntityId = null)
    {
        // 1. Persist to DB
        var notification = NotificationItem.Create(userId, title, message, type, relatedEntityId);
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        // 2. Send via SignalR
        // We assume userId matches the JWT 'sub' claim or whatever UserIdentifier is mapped to.
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new 
        {
            Id = notification.Id,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = notification.CreatedAt,
            IsRead = false
        });
    }
}

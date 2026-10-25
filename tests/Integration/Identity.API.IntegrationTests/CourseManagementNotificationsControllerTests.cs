using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.API.Controllers;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementNotificationsControllerTests
{
    [Fact]
    public async Task GetMyNotifications_ReturnsOnlyCourseManagementNotificationsForCurrentUser()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Notifications.AddRange(
            NotificationItem.Create(userId, "Ders", "Planlandı", "CourseManagement.LessonScheduled"),
            NotificationItem.Create(userId, "Ödev", "Teslim edildi", "CourseManagement.AssignmentPublished"),
            NotificationItem.Create(userId, "Koçluk", "Yeni görev", "Coaching.AssignmentCreated"),
            NotificationItem.Create(Guid.NewGuid(), "Başka kullanıcı", "Gizli", "CourseManagement.LessonScheduled"));
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId);
        var result = await controller.GetMyNotifications(1, 100, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var notifications = Assert.IsAssignableFrom<IReadOnlyList<NotificationItem>>(response.Value);
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification => Assert.StartsWith("CourseManagement.", notification.Type, StringComparison.Ordinal));
        Assert.Equal("2", controller.Response.Headers["X-Total-Count"].ToString());
        Assert.Equal("2", controller.Response.Headers["X-Unread-Count"].ToString());
    }

    [Fact]
    public async Task MarkAsRead_CannotMutateAnotherProductsNotification()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDb();
        var coachingNotification = NotificationItem.Create(userId, "Koçluk", "Yeni görev", "Coaching.AssignmentCreated");
        db.Notifications.Add(coachingNotification);
        await db.SaveChangesAsync();
        var controller = CreateController(db, userId);

        var result = await controller.MarkAsRead(coachingNotification.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False((await db.Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public void ControllerRequiresCourseManagementAuthorizationPolicy()
    {
        var authorization = Assert.Single(typeof(CourseManagementNotificationsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());

        Assert.Equal("CourseManagementNotifications", authorization.Policy);
    }

    private static NotificationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CourseManagementNotificationsController CreateController(NotificationDbContext db, Guid userId)
    {
        var controller = new CourseManagementNotificationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", userId.ToString())], "test"))
                }
            }
        };
        return controller;
    }
}

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.API.Controllers;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class NotificationPaginationTests
{
    [Fact]
    public async Task GetMyNotifications_ShouldReturnBoundedPageAndMetadata()
    {
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new NotificationDbContext(options);
        for (var index = 0; index < 3; index++)
        {
            var notification = NotificationItem.Create(userId, $"Title {index}", "Message", "Info");
            notification.CreatedAt = DateTime.UtcNow.AddMinutes(-index);
            dbContext.Notifications.Add(notification);
        }

        await dbContext.SaveChangesAsync();

        var controller = new NotificationsController(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                        authenticationType: "test"))
                }
            }
        };

        var actionResult = await controller.GetMyNotifications(pageNumber: 1, pageSize: 2);
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var page = okResult.Value.Should().BeAssignableTo<List<NotificationItem>>().Subject;

        page.Should().HaveCount(2);
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("3");
        controller.Response.Headers["X-Unread-Count"].ToString().Should().Be("3");
        controller.Response.Headers["X-Page-Number"].ToString().Should().Be("1");
        controller.Response.Headers["X-Page-Size"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task GetMyNotifications_ShouldRejectUnboundedPageSize()
    {
        await using var dbContext = new NotificationDbContext(
            new DbContextOptionsBuilder<NotificationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var controller = new NotificationsController(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetMyNotifications(pageSize: 101);

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var details = problem.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Status.Should().Be(StatusCodes.Status400BadRequest);
        details.Type.Should().Be("https://eduplatform.dev/problems/validation-error");
    }
}

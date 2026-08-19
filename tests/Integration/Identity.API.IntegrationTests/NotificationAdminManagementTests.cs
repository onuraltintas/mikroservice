using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Commands.ManageNotifications;
using Notification.Application.Queries;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class NotificationAdminManagementTests
{
    [Fact]
    public async Task SupportQuery_ReturnsUnprocessedRequestsWithPagination()
    {
        await using var context = CreateContext();
        context.SupportRequests.Add(new SupportRequest(
            Guid.NewGuid(), "Ada", "Test", "ada@test.local", "Giriş", "Hesabıma giriş yapamıyorum.", "idempotency-123456"));
        await context.SaveChangesAsync();

        var handler = new GetSupportRequestsQueryHandler(context);
        var result = await handler.Handle(new GetSupportRequestsQuery(1, 10, false, "ada"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(item => item.Email == "ada@test.local");
    }

    [Fact]
    public async Task ProcessSupportRequestCommand_SavesAdminNote()
    {
        await using var context = CreateContext();
        var request = new SupportRequest(
            Guid.NewGuid(), "Ada", "Test", "ada@test.local", "Giriş", "Hesabıma giriş yapamıyorum.", "idempotency-654321");
        context.SupportRequests.Add(request);
        await context.SaveChangesAsync();

        var handler = new ProcessSupportRequestCommandHandler(context);
        var result = await handler.Handle(new ProcessSupportRequestCommand(request.Id, "İncelendi"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = await context.SupportRequests.SingleAsync(item => item.Id == request.Id);
        saved.IsProcessed.Should().BeTrue();
        saved.AdminNote.Should().Be("İncelendi");
    }

    [Fact]
    public async Task EmailTemplateUpdateCommand_ChangesActiveStateAndContent()
    {
        await using var context = CreateContext();
        var template = EmailTemplate.Create("Auth_Test", "Auth", "Eski", "Eski içerik");
        context.EmailTemplates.Add(template);
        await context.SaveChangesAsync();

        var handler = new UpdateEmailTemplateCommandHandler(context);
        var result = await handler.Handle(
            new UpdateEmailTemplateCommand(template.Id, "Auth", "Yeni", "Yeni içerik", false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = await context.EmailTemplates.SingleAsync(item => item.Id == template.Id);
        saved.Subject.Should().Be("Yeni");
        saved.IsActive.Should().BeFalse();
    }

    private static NotificationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }
}

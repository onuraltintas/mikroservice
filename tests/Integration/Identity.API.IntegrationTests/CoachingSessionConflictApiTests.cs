using Coaching.API.Controllers;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSession;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSessionConflictApiTests
{
    [Fact]
    public async Task SessionCreationAndReschedulingReturn409ForBookingConflicts()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IRequestHandler<CreateSessionCommand, CreateSessionResponse>>(new CreateConflict())
            .AddSingleton<IRequestHandler<UpdateSessionCommand, UpdateSessionResponse>>(new UpdateConflict())
            .BuildServiceProvider();
        var controller = new SessionsController(new Mediator(services), NullLogger<SessionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var create = await controller.CreateSession(new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(2), 60, "Session", null, SessionType.OneOnOne), default);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(create.Result).StatusCode);
        var id = Guid.NewGuid();
        var update = await controller.UpdateSession(id, new(id, "Session", null, DateTime.UtcNow.AddDays(2), 60, null, null), default);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(update.Result).StatusCode);
    }

    private sealed class CreateConflict : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
    {
        public Task<CreateSessionResponse> Handle(CreateSessionCommand request, CancellationToken ct)
            => throw new BusinessRuleException("Session.Conflict", "Choose another time.");
    }
    private sealed class UpdateConflict : IRequestHandler<UpdateSessionCommand, UpdateSessionResponse>
    {
        public Task<UpdateSessionResponse> Handle(UpdateSessionCommand request, CancellationToken ct)
            => throw new BusinessRuleException("Session.Conflict", "Choose another time.");
    }
}

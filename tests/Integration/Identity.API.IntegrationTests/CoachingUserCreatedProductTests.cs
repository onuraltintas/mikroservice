using System.Reflection;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Consumer = Coaching.Application.Consumers.UserCreatedConsumer;

namespace Identity.API.IntegrationTests;

public sealed class CoachingUserCreatedProductTests
{
    [Theory]
    [InlineData("coaching", "Student", 1)]
    [InlineData("speed-reading", "Student", 0)]
    [InlineData("course-management", "Student", 0)]
    [InlineData("coaching", "Teacher", 0)]
    [InlineData(null, "Student", 1)]
    public async Task Welcome_goal_is_only_created_for_coaching_or_legacy_students(string? product, string role, int expectedGoals)
    {
        var goals = DispatchProxy.Create<IAcademicGoalRepository, GoalProxy>();
        var context = DispatchProxy.Create<ConsumeContext<UserCreatedEvent>, ContextProxy>();
        ((ContextProxy)(object)context).Message = new UserCreatedEvent(Guid.NewGuid(), "user@example.test", "Test", "User", role,
            "setup-token", DateTime.UtcNow.AddHours(1), DateTime.UtcNow) { Product = product };
        var work = DispatchProxy.Create<IUnitOfWork, WorkProxy>();
        await new Consumer(NullLogger<Consumer>.Instance, goals, work).Consume(context);
        Assert.Equal(expectedGoals, ((GoalProxy)(object)goals).AddCount);
        Assert.Equal(expectedGoals, ((WorkProxy)(object)work).SaveCount);
    }

    public class ContextProxy : DispatchProxy
    {
        public UserCreatedEvent Message { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class GoalProxy : DispatchProxy
    {
        public int AddCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IAcademicGoalRepository.AddAsync)) throw new NotSupportedException(method.Name);
            AddCount++; return Task.FromResult((Coaching.Domain.Entities.AcademicGoal)args![0]!);
        }
    }
    public class WorkProxy : DispatchProxy
    {
        public int SaveCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            SaveCount++; return Task.FromResult(1);
        }
    }
}

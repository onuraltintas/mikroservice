using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.DeleteGoal;
using Coaching.Application.Commands.UpdateGoal;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLegacyGoalAssignmentTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RevokedTeacherCannotEditOrDeleteHistoricalGoal(bool delete, bool dualRole)
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var teacher = Guid.NewGuid(); var student = Guid.NewGuid();
        var goal = AcademicGoal.Create(student, "Korunan hedef", GoalCategory.ExamPreparation, teacher);
        db.Add(goal); await db.SaveChangesAsync();
        var publisher = new Publisher(); var identity = new Identity(teacher, student);
        using var services = new ServiceCollection()
            .AddSingleton<IAcademicGoalRepository>(new AcademicGoalRepository(db))
            .AddSingleton<Coaching.Application.Interfaces.IUnitOfWork>(new UnitOfWork(db))
            .AddSingleton<ICoachingAccessPolicy>(new CoachingAccessPolicy(new Actor(teacher, dualRole)))
            .AddSingleton<ICoachingEventPublisher>(publisher)
            .AddSingleton<ICoachingIdentityAuthorizationClient>(identity).BuildServiceProvider();
        var failure = await Assert.ThrowsAsync<BusinessRuleException>(async () => {
            if (delete) await ActivatorUtilities.CreateInstance<DeleteGoalCommandHandler>(services).Handle(new(goal.Id), default);
            else await ActivatorUtilities.CreateInstance<UpdateGoalCommandHandler>(services).Handle(
                new(goal.Id, "Yetkisiz değişiklik", null, GoalCategory.Other, null, null, null, null), default);
        });
        Assert.Equal("Authorization.Forbidden", failure.Code); Assert.Equal(1, identity.Calls);
        db.ChangeTracker.Clear(); Assert.Equal("Korunan hedef", (await db.AcademicGoals.SingleAsync()).Title);
        Assert.Equal(0, publisher.Count);
    }

    private sealed class Actor(Guid id, bool dualRole) : ICurrentUserService
    {
        public Guid? UserId => id;
        public IEnumerable<string> Roles => dualRole ? ["Teacher", "InstitutionAdmin"] : ["Teacher"];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
    private sealed class Publisher : ICoachingEventPublisher
    {
        public int Count { get; private set; }
        public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class { Count++; return Task.CompletedTask; }
    }
    private sealed class Identity(Guid teacher, Guid student) : ICoachingIdentityAuthorizationClient
    {
        public int Calls { get; private set; }
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewer, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> students, CancellationToken ct) => throw new NotSupportedException();
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institutionId, bool global, CancellationToken ct)
        {
            Assert.Equal(teacher, teacherId); Assert.Equal(new[] { student }, studentIds); Assert.False(global); Assert.Null(institutionId);
            Calls++; throw new BusinessRuleException("Authorization.Forbidden", "Atama sona erdi.");
        }
    }
}

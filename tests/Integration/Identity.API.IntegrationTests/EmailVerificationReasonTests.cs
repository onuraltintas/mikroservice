using System.Reflection;
using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using Identity.Application.Commands.ConfirmEmail;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Paket F #10: /auth/confirm-email shows one message per reason — verified, already verified (sign in), expired or
/// invalid (ask for a new e-mail). The handler tells them apart.
/// </summary>
public sealed class EmailVerificationReasonTests
{
    [Fact]
    public async Task AValidLinkVerifiesAndTheSameLinkAgainSaysAlreadyVerified()
    {
        await using var f = await Fixture.CreateAsync();
        var token = f.User.EmailVerificationToken!;

        var first = await f.Confirm(token);
        var again = await f.Confirm(token);

        first.IsSuccess.Should().BeTrue();
        first.Value.Should().Be(ConfirmEmailOutcome.Confirmed);
        again.IsSuccess.Should().BeTrue();
        again.Value.Should().Be(ConfirmEmailOutcome.AlreadyConfirmed);
        f.Published.OfType<UserEmailConfirmedEvent>().Should().ContainSingle("the welcome e-mail goes out once");
    }

    // Review L1 (kept as is): an already verified account answers "already verified" whatever token is presented;
    // the token is cleared on verification, so there is nothing left to compare it with.
    [Fact]
    public async Task AVerifiedAccountSaysAlreadyVerifiedEvenForAWrongToken()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Confirm(f.User.EmailVerificationToken!)).IsSuccess.Should().BeTrue();

        var result = await f.Confirm("not-the-token");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ConfirmEmailOutcome.AlreadyConfirmed);
    }

    [Fact]
    public async Task AnExpiredLinkIsReportedAsExpired()
    {
        await using var f = await Fixture.CreateAsync();
        typeof(User).GetProperty(nameof(User.EmailVerificationTokenExpiresAt))!.SetValue(f.User, DateTime.UtcNow.AddMinutes(-1));
        await f.Context.SaveChangesAsync();

        var result = await f.Confirm(f.User.EmailVerificationToken!);

        result.Error.Code.Should().Be("User.TokenExpired");
        (await f.Reload()).EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task AWrongOrReplacedLinkIsReportedAsInvalid()
    {
        await using var f = await Fixture.CreateAsync();

        var result = await f.Confirm("not-the-token");

        result.Error.Code.Should().Be("User.InvalidToken");
        (await f.Reload()).EmailConfirmed.Should().BeFalse();
        f.Published.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public IdentityDbContext Context { get; private init; } = null!;
        public User User { get; private init; } = null!;
        public List<object> Published { get; } = [];

        public static async Task<Fixture> CreateAsync()
        {
            var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var user = User.Create(Guid.NewGuid(), "ogrenci@example.test", "Ada", "Kaya");
            user.GenerateEmailVerificationToken();
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return new Fixture { Context = context, User = user };
        }

        public Task<EduPlatform.Shared.Kernel.Results.Result<ConfirmEmailOutcome>> Confirm(string token) =>
            new ConfirmEmailCommandHandler(new UserRepository(Context), new UnitOfWork(Context), Publisher())
                .Handle(new ConfirmEmailCommand(User.Id, token, "coaching"), default);

        public async Task<User> Reload()
        {
            Context.ChangeTracker.Clear();
            return await Context.Users.SingleAsync(u => u.Id == User.Id);
        }

        private IPublishEndpoint Publisher()
        {
            var proxy = DispatchProxy.Create<IPublishEndpoint, AccountEmailChangeTests.PublishProxy>();
            ((AccountEmailChangeTests.PublishProxy)(object)proxy).Published = Published;
            return proxy;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}

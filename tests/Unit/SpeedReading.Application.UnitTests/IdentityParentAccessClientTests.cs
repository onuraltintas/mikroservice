using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.Infrastructure.ExternalServices;

namespace SpeedReading.Application.UnitTests;

/// <summary>V2: every parent read asks Identity's authorize-read for the SpeedReading product.</summary>
public sealed class IdentityParentAccessClientTests
{
    private const string Key = "a-secure-service-key-with-at-least-32-bytes";

    [Fact]
    public async Task Asks_identity_with_the_internal_key_and_the_speed_reading_product()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"relationshipId":"8c7f2c55-3f8b-4ad8-9cf3-5f4e8f0f7a10","relationship":"Mother"}""",
                System.Text.Encoding.UTF8, "application/json")
        });
        var parent = Guid.NewGuid(); var student = Guid.NewGuid();

        var allowed = await Client(handler).CanReadAsync(parent, student);

        allowed.Should().BeTrue();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("http://identity-service:8080/api/internal/parents/authorize-read");
        handler.Request.Headers.GetValues("X-Internal-Service-Key").Should().ContainSingle(Key);
        using var body = JsonDocument.Parse(handler.Payload!);
        body.RootElement.GetProperty("parentUserId").GetGuid().Should().Be(parent);
        body.RootElement.GetProperty("studentUserId").GetGuid().Should().Be(student);
        body.RootElement.GetProperty("product").GetString().Should().Be("SpeedReading");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Refusals_mean_no_access(HttpStatusCode status)
    {
        var allowed = await Client(new Handler(_ => new HttpResponseMessage(status))).CanReadAsync(Guid.NewGuid(), Guid.NewGuid());
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_ids_never_reach_identity()
    {
        var handler = new Handler(_ => throw new InvalidOperationException("must not be called"));
        (await Client(handler).CanReadAsync(Guid.Empty, Guid.NewGuid())).Should().BeFalse();
        (await Client(handler).CanReadAsync(Guid.NewGuid(), Guid.Empty)).Should().BeFalse();
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task Transport_failures_and_server_errors_are_not_treated_as_a_refusal()
    {
        await FluentActions.Awaiting(() => Client(new Handler(_ => throw new HttpRequestException("down")))
                .CanReadAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => Client(new Handler(_ => throw new TaskCanceledException("timeout")))
                .CanReadAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => Client(new Handler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)))
                .CanReadAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_missing_service_key_fails_closed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:IdentityService"] = "http://identity-service:8080"
        }).Build();
        var client = new IdentityParentAccessClient(new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            configuration, NullLogger<IdentityParentAccessClient>.Instance);
        await FluentActions.Awaiting(() => client.CanReadAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private static IdentityParentAccessClient Client(Handler handler) => new(
        new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:IdentityService"] = "http://identity-service:8080",
            ["INTERNAL_SERVICE_API_KEY"] = Key
        }).Build(),
        NullLogger<IdentityParentAccessClient>.Instance);

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Payload = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}

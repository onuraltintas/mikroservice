using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Gateway.UnitTests.Infrastructure;

/// <summary>
/// A real Kestrel server that plays every downstream service. Each cluster is mapped to
/// <c>{BaseAddress}/{cluster}</c>, so the echoed path reveals which cluster YARP selected.
/// </summary>
public sealed class BackendStub : IAsyncDisposable
{
    private readonly WebApplication _app;

    private BackendStub(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    /// <summary>Requests received by the stub, newest last.</summary>
    public ConcurrentQueue<ReceivedRequest> Requests { get; } = new();

    public static async Task<BackendStub> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);
        var app = builder.Build();
        BackendStub? stub = null;

        app.Map("/{**path}", async (HttpContext context) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            long bodyLength = 0;
            var buffer = new byte[81920];
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                bodyLength += read;
            }

            var headers = context.Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
            stub!.Requests.Enqueue(new ReceivedRequest(context.Request.Method, path, headers, bodyLength));

            if (int.TryParse(context.Request.Query["delayMs"], out var delayMs) && delayMs > 0)
            {
                await Task.Delay(delayMs, context.RequestAborted);
            }

            if (int.TryParse(context.Request.Query["status"], out var status))
            {
                context.Response.StatusCode = status;
            }

            await context.Response.WriteAsJsonAsync(new EchoResponse(
                context.Request.Method,
                path,
                headers,
                bodyLength));
        });

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        stub = new BackendStub(app, address.TrimEnd('/'));
        return stub;
    }

    public ReceivedRequest? LastRequestTo(string pathSuffix) =>
        Requests.LastOrDefault(request => request.Path.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase));

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

public sealed record ReceivedRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    long BodyLength);

public sealed record EchoResponse(
    string Method,
    string Path,
    Dictionary<string, string> Headers,
    long BodyLength)
{
    public static async Task<EchoResponse> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EchoResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Backend echo was empty: {json}");
    }

    /// <summary>The cluster that served the request, derived from the per-cluster path prefix.</summary>
    public string Cluster => Path.TrimStart('/').Split('/')[0];
}

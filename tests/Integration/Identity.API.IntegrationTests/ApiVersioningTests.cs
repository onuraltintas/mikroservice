using Asp.Versioning;
using EduPlatform.Shared.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class ApiVersioningTests
{
    [Fact]
    public void SharedApiVersioning_ShouldExposeStableV1DefaultsAndHeaderQueryReaders()
    {
        var services = new ServiceCollection();
        services.AddEduPlatformApiVersioning();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        options.DefaultApiVersion.Should().Be(new ApiVersion(1, 0));
        options.AssumeDefaultVersionWhenUnspecified.Should().BeTrue();
        options.ReportApiVersions.Should().BeTrue();
        options.ApiVersionReader.Should().NotBeNull();
    }
}

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.Application.Subscription;

namespace Identity.API.IntegrationTests;

public sealed class SubscriptionRequirementMvcBindingTests
{
    [Theory]
    [InlineData("{}", HttpStatusCode.BadRequest)]
    [InlineData("{\"requireActiveSubscription\":null}", HttpStatusCode.BadRequest)]
    [InlineData("{\"requireActiveSubscription\":false}", HttpStatusCode.OK)]
    [InlineData("{\"requireActiveSubscription\":true}", HttpStatusCode.OK)]
    public async Task RealMvcRequiresAnExplicitBooleanAndAcceptsBothStates(string json, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(SubscriptionRequirementContractProbeController).Assembly);
        await using var app = builder.Build(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PutAsync("/subscription-requirement-contract-probe", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(expected, response.StatusCode);
    }
}

[ApiController, Route("subscription-requirement-contract-probe")]
public sealed class SubscriptionRequirementContractProbeController : ControllerBase
{
    [HttpPut] public IActionResult Update(UpdateSubscriptionRequirementRequest request) => Ok(request);
}

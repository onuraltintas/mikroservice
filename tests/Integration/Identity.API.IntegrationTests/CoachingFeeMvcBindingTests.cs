using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Coaching.Application.Finance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingFeeMvcBindingTests
{
    [Theory]
    [InlineData("payment", """{"requestId":"da9dfc87-4390-4e39-a46a-ec468c80745b","expectedVersion":0,"amount":100,"method":"Cash","receivedOn":"2026-10-13","reference":null}""")]
    [InlineData("reversal", """{"requestId":"da9dfc87-4390-4e39-a46a-ec468c80745b","expectedVersion":0,"receivedOn":"2026-10-13","reason":"Düzeltme"}""")]
    [InlineData("cancel", """{"expectedVersion":0,"reason":"İptal"}""")]
    public async Task FinancialRecordsBindValidJsonAndRejectMissingOrInvalidVersions(string route, string json)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(CoachingFeeContractProbeController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        await using var app = builder.Build(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        async Task<HttpResponseMessage> Send(string body) => await client.PostAsync($"/fee-contract-probe/{route}", new StringContent(body, Encoding.UTF8, "application/json"));
        using var valid = await Send(json); Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var missing = JsonNode.Parse(json)!.AsObject(); missing.Remove("expectedVersion");
        using var invalid = await Send(missing.ToJsonString()); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        missing["expectedVersion"] = -1;
        using var negative = await Send(missing.ToJsonString()); Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }
}

// A real MVC pipeline isolates the production DTO contract from database/broker startup.
[ApiController, Route("fee-contract-probe")]
public sealed class CoachingFeeContractProbeController : ControllerBase
{
    [HttpPost("payment")] public IActionResult Payment(CoachingFeePaymentInput input) => Ok();
    [HttpPost("reversal")] public IActionResult Reversal(CoachingFeeReversalInput input) => Ok();
    [HttpPost("cancel")] public IActionResult Cancel(CoachingFeeCancelInput input) => Ok();
}

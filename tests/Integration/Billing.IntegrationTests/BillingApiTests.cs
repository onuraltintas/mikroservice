using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Billing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>The real service host: who may call what, and how errors come back.</summary>
public sealed class BillingApiTests : IAsyncLifetime
{
    private const string Secret = "billing-tests-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    private const string Issuer = "eduplatform.identity";
    private const string Audience = "eduplatform.api";
    private readonly PostgresFixture _postgres = new();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await using (var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.ConnectionString).Options))
            await db.Database.MigrateAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("BILLING_CONNECTION_STRING", _postgres.ConnectionString);
            builder.UseSetting("JWT_SECRET", Secret);
            builder.UseSetting("JWT_ISSUER", Issuer);
            builder.UseSetting("JWT_AUDIENCE", Audience);
            builder.UseSetting("JWT_KEY_ID", "tests");
            builder.UseSetting("AllowedHosts", "*");
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private HttpClient Client(string? role)
    {
        var client = _factory.CreateClient();
        if (role is null) return client;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)) { KeyId = "tests" };
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Fact]
    public async Task Health_and_the_public_catalog_are_open_and_the_admin_section_is_not()
    {
        var anonymous = Client(null);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/billing/v1/catalog/coaching")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/billing/v1/catalog/unknown")).StatusCode);

        foreach (var path in new[] { "products", "plans", "seller", "bank-accounts" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/billing/v1/admin/{path}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Client("Teacher").GetAsync($"/api/billing/v1/admin/{path}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Client("SystemAdmin").GetAsync($"/api/billing/v1/admin/{path}")).StatusCode);
        }
    }

    [Fact]
    public async Task Admin_changes_settings_with_enum_names_and_a_broken_rule_comes_back_as_a_400_with_its_code()
    {
        var admin = Client("SystemAdmin");

        var settings = await admin.PutAsJsonAsync($"/api/billing/v1/admin/products/{BillingSeed.Coaching}/settings",
            new { requireSubscription = true, bankTransferApproval = "Automatic" });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Contains("\"bankTransferApproval\":\"Automatic\"", await settings.Content.ReadAsStringAsync());

        var catalog = await admin.GetStringAsync("/api/billing/v1/catalog/coaching");
        Assert.Contains("\"requireSubscription\":true", catalog);
        Assert.Contains("\"salesOpen\":false", catalog);

        var broken = await admin.PostAsJsonAsync("/api/billing/v1/admin/bank-accounts",
            new { bankName = "Banka", accountHolder = "Sahip", iban = "TR00", isActive = true, sortOrder = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Contains("Billing.BankAccountInvalid", await broken.Content.ReadAsStringAsync());
    }
}

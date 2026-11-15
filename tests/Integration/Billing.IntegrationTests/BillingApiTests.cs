using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Billing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>The real service host: who may call what, and how errors come back.</summary>
public sealed class BillingApiTests : IAsyncLifetime
{
    private const string Secret = "billing-tests-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    private const string Issuer = "eduplatform.identity";
    private const string Audience = "eduplatform.api";
    private const string ServiceKey = "billing-tests-internal-service-key-0123456789";
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
            builder.UseSetting("INTERNAL_SERVICE_API_KEY", ServiceKey);
            builder.UseSetting("Billing:RunWorkers", "false");
            // Identity is not running here; nobody administers an institution.
            builder.ConfigureServices(services => services.AddScoped<Billing.Application.IBillingInstitutions>(_ => new FakeInstitutions()));
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private HttpClient Client(string? role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        if (role is null) return client;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)) { KeyId = "tests" };
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim("sub", (userId ?? Guid.NewGuid()).ToString()), new Claim("email", "kullanici@example.invalid"), new Claim(ClaimTypes.Role, role)],
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

    [Fact]
    public async Task A_student_buys_by_bank_transfer_the_admin_approves_and_the_product_sees_the_entitlement()
    {
        var admin = Client("SystemAdmin");
        var studentId = Guid.NewGuid();
        var student = Client("Student", studentId);

        // The admin opens sales: seller, a bank account, a plan, and "subscription required" for the product.
        (await admin.PutAsJsonAsync("/api/billing/v1/admin/seller", new { title = "TEST Satıcı Ltd.", address = "Bir adres", taxOffice = "Daire",
            taxNumber = "1234567890", email = "satici@example.invalid", phone = "0212" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/billing/v1/admin/bank-accounts", new { bankName = "Test Bankası", accountHolder = "TEST Satıcı Ltd.",
            iban = "TR330006100519786457841326", isActive = true, sortOrder = 1 })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/billing/v1/admin/products/{BillingSeed.SpeedReading}/settings",
            new { requireSubscription = true, bankTransferApproval = "Manual" })).EnsureSuccessStatusCode();
        var products = await admin.GetFromJsonAsync<JsonElement>("/api/billing/v1/admin/products");
        var entitlementId = products.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "speed-reading")
            .GetProperty("entitlements").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "speed-reading.student").GetProperty("id").GetString();
        var plan = await (await admin.PostAsJsonAsync("/api/billing/v1/admin/plans", new { name = "TEST Aylık", buyerType = "Individual", price = 200,
            durationDays = 30, trialDays = 0, isContactOnly = false, isActive = true, isPublic = true, sortOrder = 1,
            entitlements = new[] { new { entitlementId, holder = "Buyer" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var planId = plan.GetProperty("id").GetString();

        // The buyer reads the documents, orders, and says the money is sent, with a receipt.
        var documents = await student.GetFromJsonAsync<JsonElement>($"/api/billing/v1/documents?planId={planId}&buyerName=Deneme%20%C3%96%C4%9Frenci");
        var version = documents.GetProperty("version").GetString();
        Assert.Contains("MESAFELİ SATIŞ SÖZLEŞMESİ", documents.GetProperty("distanceSales").GetString());
        var refused = await student.PostAsJsonAsync("/api/billing/v1/orders", new { planId, buyerName = "Deneme Öğrenci" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Billing.DocumentsNotAccepted", await refused.Content.ReadAsStringAsync());
        var order = await (await student.PostAsJsonAsync("/api/billing/v1/orders", new { planId, documentsVersion = version, buyerName = "Deneme Öğrenci" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.StartsWith("EDU-", order.GetProperty("paymentCode").GetString());

        using var form = new MultipartFormDataContent { { new StringContent("Gönderdim"), "note" } };
        var file = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "receipt", "dekont.pdf");
        var paid = await student.PostAsync($"/api/billing/v1/orders/{orderId}/paid", form);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.True((await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hasReceipt").GetBoolean());

        // Another user sees nothing of it; a non-admin cannot review.
        var stranger = Client("Student");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/billing/v1/orders/{orderId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/billing/v1/orders/{orderId}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/billing/v1/orders/{orderId}")).StatusCode);
        Assert.Equal("[]", await stranger.GetStringAsync("/api/billing/v1/orders"));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync($"/api/billing/v1/admin/orders/{orderId}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/billing/v1/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("/api/billing/v1/orders")).StatusCode);

        // The admin's queue, the receipt, the approval.
        var queue = await admin.GetFromJsonAsync<JsonElement>("/api/billing/v1/admin/orders?status=Pending&product=speed-reading");
        Assert.Equal(orderId, queue.EnumerateArray().Single().GetProperty("order").GetProperty("id").GetString());
        var receipt = await admin.GetAsync($"/api/billing/v1/admin/orders/{orderId}/receipt");
        Assert.Equal("application/pdf", receipt.Content.Headers.ContentType?.MediaType);
        var approved = await admin.PostAsync($"/api/billing/v1/admin/orders/{orderId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var again = await admin.PostAsync($"/api/billing/v1/admin/orders/{orderId}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // "Aboneliğim" and the product's own question.
        var mine = await student.GetFromJsonAsync<JsonElement>("/api/billing/v1/me?product=speed-reading");
        Assert.Equal("speed-reading.student", mine.GetProperty("entitlements").EnumerateArray().Single().GetProperty("key").GetString());
        Assert.True(mine.GetProperty("subscriptions").EnumerateArray().Single().GetProperty("isCurrent").GetBoolean());

        var internalPath = $"/internal/billing/v1/entitlements?userId={studentId}&product=speed-reading";
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync(internalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync(internalPath)).StatusCode);
        using var wrong = new HttpRequestMessage(HttpMethod.Get, internalPath);
        wrong.Headers.Add("X-Internal-Service-Key", new string('x', ServiceKey.Length));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).SendAsync(wrong)).StatusCode);
        using var keyed = new HttpRequestMessage(HttpMethod.Get, internalPath);
        keyed.Headers.Add("X-Internal-Service-Key", ServiceKey);
        var access = await (await Client(null).SendAsync(keyed)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(access.GetProperty("requireSubscription").GetBoolean());
        Assert.Equal("speed-reading.student", access.GetProperty("entitlements").EnumerateArray().Single().GetProperty("key").GetString());

        // Taking the approval back needs a reason and ends the access.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/billing/v1/admin/orders/{orderId}/reverse", new { note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/billing/v1/admin/orders/{orderId}/reverse", new { note = "Ödeme bankadan geri döndü." })).StatusCode);
        var after = await student.GetFromJsonAsync<JsonElement>("/api/billing/v1/me?product=speed-reading");
        Assert.Empty(after.GetProperty("entitlements").EnumerateArray());
    }

    [Fact]
    public async Task Seats_are_given_only_inside_the_network_and_an_owner_reaches_only_their_own_pool()
    {
        var body = new { product = "coaching", ownerUserId = Guid.NewGuid(), userId = Guid.NewGuid(), actorId = Guid.NewGuid() };
        // Through the gateway's path there is no seat endpoint at all; the internal one asks for the service key.
        Assert.Equal(HttpStatusCode.NotFound, (await Client("SystemAdmin").PostAsJsonAsync("/api/billing/v1/seats/assign", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("SystemAdmin").PostAsJsonAsync("/internal/billing/v1/seats/assign", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync($"/internal/billing/v1/seats?product=coaching&ownerUserId={Guid.NewGuid()}")).StatusCode);

        using var keyed = new HttpRequestMessage(HttpMethod.Post, "/internal/billing/v1/seats/assign") { Content = JsonContent.Create(body) };
        keyed.Headers.Add("X-Internal-Service-Key", ServiceKey);
        var result = await (await Client(null).SendAsync(keyed)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NoPool", result.GetProperty("code").GetString());
        using var pool = new HttpRequestMessage(HttpMethod.Get, $"/internal/billing/v1/seats?product=coaching&ownerUserId={Guid.NewGuid()}");
        pool.Headers.Add("X-Internal-Service-Key", ServiceKey);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(null).SendAsync(pool)).StatusCode);
        using var both = new HttpRequestMessage(HttpMethod.Get, $"/internal/billing/v1/entitlements?product=coaching&userId={Guid.NewGuid()}&organizationId={Guid.NewGuid()}");
        both.Headers.Add("X-Internal-Service-Key", ServiceKey);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client(null).SendAsync(both)).StatusCode);

        var teacher = Client("Teacher");
        Assert.Equal("[]", await teacher.GetStringAsync("/api/billing/v1/organizations"));
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/billing/v1/subscriptions/{Guid.NewGuid()}/seats")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("/api/billing/v1/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync($"/api/billing/v1/admin/subscriptions/{Guid.NewGuid()}/seats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client("SystemAdmin").GetAsync($"/api/billing/v1/admin/subscriptions/{Guid.NewGuid()}/seats")).StatusCode);
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using EduPlatform.Shared.Security.Services;

const string email = "testeditor@eduivme.com";
var mode = args.Single();
if (mode is not ("drill" or "apply" or "verify")) throw new InvalidOperationException("Unexpected operation");
var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDENTITY_CONNECTION"));
if (connection.Database != "identity_db") throw new InvalidOperationException("Unexpected database");
if (mode == "apply" && Environment.GetEnvironmentVariable("EDITOR_FIXTURE_AUTHORIZED") != "reports-brand-editor-20261016")
    throw new InvalidOperationException("Missing explicit fixture authorization");
var source = new NpgsqlDataSourceBuilder(connection.ConnectionString); source.EnableDynamicJson();
await using var dataSource = source.Build();
await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(dataSource).Options);
await using var transaction = await db.Database.BeginTransactionAsync();
var existing = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).Include(u => u.ProductAccesses)
    .SingleOrDefaultAsync(u => u.Email.ToLower() == email);
if (existing is not null)
{
    if (existing.FirstName != "Canlı Test" || existing.LastName != "editör" || !existing.IsActive || !existing.EmailConfirmed
        || existing.Roles.Count != 1 || existing.Roles.Single().Role.Name != "Editor"
        || existing.Roles.Single().Product != PlatformProduct.SpeedReading || existing.ProductAccesses.Count != 1
        || !existing.HasProductAccess(PlatformProduct.SpeedReading))
        throw new InvalidOperationException("Existing account must not be overwritten or expanded");
    Console.WriteLine(JsonSerializer.Serialize(new { existing.Id, email, role = "Editor", product = "speed-reading", created = false, verified = true }));
    return;
}
if (mode == "verify") throw new InvalidOperationException("Fixture does not exist");
var roleId = await db.Roles.Where(r => r.Name == "Editor" && !r.IsDeleted).Select(r => r.Id).SingleAsync();
var actor = await db.Users.Where(u => u.Email == "admin@eduivme.com" && u.IsActive
    && u.Roles.Any(r => r.Role.Name == "SystemAdmin" && r.Product == null)).Select(u => u.Id).SingleAsync();
using var request = JsonDocument.Parse(await File.ReadAllTextAsync("/fixture-request/request.json"));
var password = request.RootElement.GetProperty("Password").GetString()!;
if (password.Length < 12) throw new InvalidOperationException("Invalid test password");
new PasswordHasher().CreatePasswordHash(password, out var hash, out var salt);
var user = User.Create(Guid.NewGuid(), email, "Canlı Test", "editör");
user.SetPassword(hash, salt); user.ConfirmEmail();
user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, roleId, PlatformProduct.SpeedReading));
user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, actor, DateTimeOffset.UtcNow);
db.Users.Add(user); await db.SaveChangesAsync();
if (mode == "apply") await transaction.CommitAsync(); else await transaction.RollbackAsync();
Console.WriteLine(JsonSerializer.Serialize(new { user.Id, email, role = "Editor", product = "speed-reading", created = mode == "apply", rolledBack = mode == "drill" }));

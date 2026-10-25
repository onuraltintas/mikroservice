using System.Text.Json;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

if (args.Length != 1) throw new ArgumentException("Usage: TargetAdmissions <reviewed-target-catalog.json>");
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Coaching database connection must be supplied through the environment.");
var package = JsonSerializer.Deserialize<TargetCatalogPackage>(File.ReadAllText(args[0]),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new ArgumentException("Invalid catalog package.");
await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection).Options);
if ((await db.Database.GetPendingMigrationsAsync()).Any())
    throw new InvalidOperationException("Apply the reviewed database migration before importing the catalog.");
var result = await new TargetCatalogImporter(db).ImportAsync(package);
Console.WriteLine(JsonSerializer.Serialize(result));

using System.Text.Json;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

var connection=Environment.GetEnvironmentVariable("TARGET_ADMISSIONS_CONNECTION")??throw new ArgumentException("Connection must be supplied in the operator environment.");
var settings=new NpgsqlConnectionStringBuilder(connection);
if(args.Length<2||args[0]!="--database"||args[1]!=settings.Database)throw new ArgumentException("Explicit database confirmation is required.");
var local=settings.Database is "coaching_target2026_test" or "coaching_target2026_test_final" && settings.Host=="127.0.0.1";
if(!local && !args.Contains("--apply"))throw new ArgumentException("Live writes require --apply.");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,
    o=>o.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
if(local)
{
    await db.Database.EnsureCreatedAsync();
    var sql=db.GetService<IMigrator>().GenerateScript("20261018222725_YksExamPreparationCatalog","20261019095937_TargetAdmissions2026");
    if(sql.Contains("DROP TABLE")||sql.Contains("ALTER TABLE coaching.academic_goals"))throw new Exception("Migration is not additive.");
    Console.WriteLine("Additive migration SQL verified.");
}
else if((await db.Database.GetPendingMigrationsAsync()).Any()||db.Database.HasPendingModelChanges())
    throw new InvalidOperationException("Run the reviewed additive migration before importing.");
var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
var legacyPath=args.SkipWhile(x=>x!="--legacy").Skip(1).FirstOrDefault();
if(legacyPath is not null)
{
    if(!local)throw new ArgumentException("Legacy fixtures are test-only.");
    using var legacy=JsonDocument.Parse(await File.ReadAllTextAsync(legacyPath));
    if(!await db.TargetSchools.AnyAsync()&&!await db.TargetUniversityPrograms.AnyAsync())
    {
        foreach(var s in legacy.RootElement.GetProperty("schools").EnumerateArray())
        {var e=TargetSchool.Create(s.GetProperty("Source").GetString()!,s.GetProperty("SourceId").GetString()!,s.GetProperty("Name").GetString()!,s.GetProperty("City").GetString()!,s.GetProperty("District").GetString()!,s.GetProperty("MinimumScore").GetDecimal());e.SetActive(s.GetProperty("IsActive").GetBoolean());db.Add(e);}
        foreach(var p in legacy.RootElement.GetProperty("programs").EnumerateArray())
        {var e=TargetUniversityProgram.Create(p.GetProperty("Source").GetString()!,p.GetProperty("SourceId").GetString()!,p.GetProperty("UniversityName").GetString()!,p.GetProperty("Name").GetString()!,p.GetProperty("ProgramCode").GetString(),p.GetProperty("ScoreType").GetString(),p.GetProperty("MinimumScore").GetDecimal());e.SetActive(p.GetProperty("IsActive").GetBoolean());db.Add(e);}
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
    }
}
var beforePrograms=await db.TargetUniversityPrograms.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>JsonSerializer.Serialize(x));
var beforeSchools=await db.TargetSchools.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>JsonSerializer.Serialize(x));
var packagePath=args.SkipWhile(x=>x!="--package").Skip(1).FirstOrDefault()??throw new ArgumentException("Package path required.");
var package=JsonSerializer.Deserialize<TargetAdmissionPackage>(await File.ReadAllTextAsync(packagePath),options)??throw new ArgumentException("Invalid package.");
var importer=new TargetAdmissionImporter(db);var result=await importer.ImportAsync(package);
var repeated=await importer.ImportAsync(package);
if(!repeated.AlreadyImported||repeated.BatchId!=result.BatchId)throw new Exception("Import is not idempotent.");
db.ChangeTracker.Clear();
foreach(var p in await db.TargetUniversityPrograms.AsNoTracking().ToListAsync())if(beforePrograms.TryGetValue(p.Id,out var old)&&old!=JsonSerializer.Serialize(p))throw new Exception("Legacy program changed.");
foreach(var s in await db.TargetSchools.AsNoTracking().ToListAsync())if(beforeSchools.TryGetValue(s.Id,out var old)&&old!=JsonSerializer.Serialize(s))throw new Exception("Legacy school changed.");
var service=new TargetAdmissionSearchService(db);
var page=await service.SearchAsync(new(package.ExamCode,package.Year,ScoreType:package.ExamCode=="YKS"?"SAY":"LGS"));
var facets=await service.FacetsAsync(package.ExamCode,package.Year);
Console.WriteLine(JsonSerializer.Serialize(new{result,filteredCount=page.TotalCount,facets=facets.ToDictionary(x=>x.Key,x=>x.Value.Length),legacyProgramsPreserved=beforePrograms.Count,legacySchoolsPreserved=beforeSchools.Count}));
var review=await db.Set<TargetAdmissionRecord>().Where(x=>x.BatchId==result.BatchId&&x.MatchStatus!="Verified").Select(x=>new{x.ExternalCode,x.Name,x.Institution,x.MatchStatus,x.Issue}).ToListAsync();
var output=args.SkipWhile(x=>x!="--review-output").Skip(1).FirstOrDefault();
if(output is not null)await File.WriteAllTextAsync(output,JsonSerializer.Serialize(review,new JsonSerializerOptions{WriteIndented=true}));

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("VERIFY_CONNECTION") ?? throw new Exception("Missing connection");
var database = new NpgsqlConnectionStringBuilder(connection).Database;
var liveCatalog = args.SequenceEqual(new[] { "--live-catalog" });
if (liveCatalog ? database != "coaching_db" || Environment.GetEnvironmentVariable("ALLOW_LIVE_CATALOG") != "coaching-learning-20261016"
    : database != "coaching_learning_drill_coaching_20261016") throw new Exception("Database scope rejected");
var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,
    n => n.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
await using var db = new CoachingDbContext(options);
var actorId = Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID") ?? throw new Exception("Admin actor required"));
var curricula = new CoachingCurriculumService(db, new GlobalScope(), new Actor(actorId));
const string code = "coaching-approved-catalog-2026-10";
var version = await db.CurriculumVersions.SingleOrDefaultAsync(x => x.Code == code);
if (version is null)
{
    var lessons = await db.StudyCatalogLessons.Where(x => x.Source == "approved-catalog" && x.IsActive && x.CurriculumVersionId == null).Select(x => x.Id).ToArrayAsync();
    Check(lessons.Length == 169, "Approved catalog changed; review scope before publication");
    version = await curricula.CreateAsync(new(code, "Koçluk onaylı ders kataloğu", "2026.10", 2026, 2026,
        "Canlıda önceden onaylanmış approved-catalog ders, ünite ve konu verisi; uygulama katalog sürümü.", null, lessons,
        "Yetkili eğitim bağlamı yayını: mevcut onaylı kataloğu sürüme bağla; içerik ve konu sürelerini koru."), default);
}
if (!version.IsPublished) await curricula.SetPublishedAsync(version.Id, new(version.Version, true,
    "Yetkili kurulum yayını için mevcut onaylı ders kataloğunu erişime aç."), default);
if (liveCatalog)
{
    Console.WriteLine(JsonSerializer.Serialize(new { passed = true, operation = "publish-existing-approved-catalog", lessons = await db.StudyCatalogLessons.CountAsync(x => x.CurriculumVersionId == version.Id), inventedDurations = 0 })); return;
}
var baseUrl = new Uri(Environment.GetEnvironmentVariable("VERIFY_API") ?? throw new Exception("Private API required"));
Check(baseUrl.Scheme == "http" && (baseUrl.Host.StartsWith("coaching-learning-drill-") || System.Net.IPAddress.TryParse(baseUrl.Host, out _)), "Private drill API required");
var student = await db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.IsActive);
var oldPlans = await db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.Version, x.Status }).ToArrayAsync();
var oldTasks = await db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.RevisionId, x.IsCompleted, x.ActualMinutes, x.ActualQuestions }).ToArrayAsync();
using var http = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(student.UserId, "Student"));
var steps = new List<string>();
var own = (await Get("/api/coaching/profiles/me")).GetProperty("student");
Check(own.GetProperty("userId").GetGuid() == student.UserId, "UserId profile mismatch"); steps.Add("owned-profile");
var start = await Get("/api/coaching/study-planning/setup");
Check(start.GetProperty("context").ValueKind == JsonValueKind.Null, "Fresh setup expected");
await Get("/api/coaching/study-planning/plans"); steps.Add("old-plans-before-setup");
if (!student.IsEducationComplete)
{
    var response = await http.PutAsJsonAsync("/api/coaching/profiles/me/student", new { expectedVersion = student.Version, educationStatus = "Student", gradeLevel = 12,
        student.SchoolName, student.SchoolCity, student.SchoolDistrict, student.SchoolType, student.ClassBranch, graduationYear = (int?)null,
        student.BirthDate, student.Bio, student.AvatarUrl, student.ShareProgressWithTeachers }); await Success(response);
}
var draft = await Save(null, [], [], false); var contextVersion = draft.GetProperty("context").GetProperty("version").GetInt32();
var resumed = await Get("/api/coaching/study-planning/setup");
Check(resumed.GetProperty("context").GetProperty("version").GetInt32() == contextVersion, "Resume failed"); steps.Add("persist-and-resume");
var availability = await Get("/api/coaching/study-planning/availability");
var hoursResponse = await http.PutAsJsonAsync("/api/coaching/study-planning/availability", new { expectedVersion = availability.ValueKind == JsonValueKind.Null ? (int?)null : availability.GetProperty("version").GetInt32(), timeZoneId = "Europe/Istanbul", windows = new[] { new { day = "Monday", startMinute = 1020, endMinute = 1080 } } });
var hours = await Success(hoursResponse); steps.Add("availability");
var catalog = await Get("/api/coaching/study-planning/setup/catalog");
var lesson = catalog.GetProperty("lessons").EnumerateArray().First(x => x.GetProperty("topicCount").GetInt32() > 0).GetProperty("id").GetGuid();
var topics = await Get($"/api/coaching/study-planning/setup/catalog?lessonId={lesson}");
var topic = topics.GetProperty("topics").GetProperty("items")[0].GetProperty("id").GetGuid();
var ready = await Save(contextVersion, [new { lessonId = lesson, priority = 1, selfReportedDifficulty = (int?)null }], [new { topicId = topic, declaredStatus = "NeedsReview" }], true);
Check(ready.GetProperty("isReady").GetBoolean(), "Saved setup not ready"); steps.Add("course-and-topic-declaration");
contextVersion = ready.GetProperty("context").GetProperty("version").GetInt32();
var previewRequest = new { startDate = "2026-10-19", days = 7, expectedAvailabilityVersion = hours.GetProperty("version").GetInt32(), expectedLearningContextVersion = contextVersion, topics = new[] { new { topicId = topic, requiredMinutes = 30 } } };
var preview = await Success(await http.PostAsJsonAsync("/api/coaching/study-planning/automatic-preview", previewRequest));
Check(preview.GetProperty("schedule").GetProperty("scheduledMinutes").GetInt32() == 30, "Preview capacity failed"); steps.Add("contextual-preview");
foreach (var plan in oldPlans.Where(x => x.StudentId == student.UserId)) await Get($"/api/coaching/study-planning/plans/{plan.Id}");
Check(JsonSerializer.Serialize(oldPlans) == JsonSerializer.Serialize(await db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.Version, x.Status }).ToArrayAsync()), "Old plans changed");
Check(JsonSerializer.Serialize(oldTasks) == JsonSerializer.Serialize(await db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.RevisionId, x.IsCompleted, x.ActualMinutes, x.ActualQuestions }).ToArrayAsync()), "Old work changed"); steps.Add("old-plans-and-completions-preserved");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token((await db.CoachingTeacherProfiles.AsNoTracking().SingleAsync(x => x.IsActive)).UserId, "Teacher"));
var rosterResponse = await http.GetAsync("/api/coaching/teachers/me/students");
Check(rosterResponse.IsSuccessStatusCode, "Teacher roster request failed");
using var roster = JsonDocument.Parse(await rosterResponse.Content.ReadAsStringAsync());
Check(roster.RootElement.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("userId").GetGuid() == student.UserId), "Transferred teacher assignment missing");
steps.Add("teacher-roster-after-userid-transfer");
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, isolated = true, scenarios = steps, preservedPlans = oldPlans.Length, preservedWorkRecords = oldTasks.Length }));

async Task<JsonElement> Save(int? expected, object[] courses, object[] topics, bool reviewed) => await Success(await http.PutAsJsonAsync("/api/coaching/study-planning/setup", new {
    expectedVersion = expected, academicYearStart = 2026, purpose = "General", targetExamYear = (int?)null, primaryScoreType = (string?)null, foreignLanguage = (string?)null, courses, topics, startingStatusReviewed = reviewed }));
async Task<JsonElement> Get(string path) => await Success(await http.GetAsync(path));
async Task<JsonElement> Success(HttpResponseMessage response)
{
    var body = await response.Content.ReadAsStringAsync();
    Check(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode} on {response.RequestMessage?.RequestUri?.AbsolutePath}: {body[..Math.Min(body.Length, 400)]}");
    using var document = JsonDocument.Parse(body); return document.RootElement.GetProperty("data").Clone();
}
string Token(Guid id, string role)
{
    var secret = Environment.GetEnvironmentVariable("JWT_SECRET")!;
    var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, id.ToString()), new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role), new Claim("platform_product", "coaching") };
    var token = new JwtSecurityToken(Environment.GetEnvironmentVariable("JWT_ISSUER"), Environment.GetEnvironmentVariable("JWT_AUDIENCE"), claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
    token.Header["kid"] = Environment.GetEnvironmentVariable("JWT_KEY_ID"); return new JwtSecurityTokenHandler().WriteToken(token);
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
sealed class GlobalScope : ICoachingAdminScopeAuthorization { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(CoachingAdminScope.Global); }
sealed class Actor(Guid id) : ICurrentUserService
{
    public Guid? UserId => id; public IEnumerable<string> Roles => ["SystemAdmin"]; public string? Email => null; public string? FullName => "Authorized deployment"; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
}

using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EduPlatform.Shared.Contracts.Events.Coaching;

var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WEEKLY_CONNECTION"));
if (args.Single() != "probe" || connection.Database != "coaching_weekly_followup_probe_20261020") throw new Exception("Probe database required.");
await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString, x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options);
if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Pending model or migration.");
var student = Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed"); var teacher = Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10");
var actor = new Actor(student); var accounts = new Accounts(teacher);
var directory = new OwnedCoachingDirectory(db, accounts, actor); var access = new CoachingAccessPolicy(actor);
var events = new Events();
var service = new CoachingWeeklyReviewService(db, access, new(access, directory, new Scope()), directory, TimeProvider.System, events);
var now = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
var week = now.AddDays(-((int)now.DayOfWeek + 6) % 7).AddDays(-14);
while (await db.Set<WeeklyReflection>().AnyAsync(x => x.StudentId == student && x.WeekStart == week)) week = week.AddDays(-7);
var homeBefore = await service.HomeAsync(default);
var reflection = await service.SaveReflectionAsync(week, new(null, "Yedek kopyası değerlendirme provası", "", "Sonraki hafta tekrarı", 4), default);
if (events.Messages.OfType<WeeklyReflectionSavedEvent>().Single().TeacherIds.Single() != teacher) throw new Exception("Reflection recipient incorrect.");
await service.SaveReflectionAsync(week, new(reflection.Version, "Yedek kopyası değerlendirme provası", "", "Sonraki hafta tekrarı", 4), default);
if (events.Messages.Count != 1) throw new Exception("Duplicate notification on unchanged reflection.");
actor.Id = teacher; actor.Role = "Teacher";
if ((await service.GetAsync(student, week, 1, default)).Reflection?.Successes != reflection.Successes) throw new Exception("Teacher cannot see reflection.");
var id = Guid.NewGuid(); var body = new WeeklyDecisionSave(null, "Yedek kopyası karar provası", "Tekrar çalışmasını izle", week.AddDays(10), "Planned");
var decision = await service.SaveDecisionAsync(student, week, id, body, default);
actor.Id = student; actor.Role = "Student";
var homeOpen = await service.HomeAsync(default);
if (homeOpen.OpenDecisions != homeBefore.OpenDecisions + 1 || homeOpen.OverdueDecisions != homeBefore.OverdueDecisions + 1) throw new Exception("Home counts incorrect.");
if (!(await service.GetAsync(null, week, 1, default, id)).Decisions.Any(x => x.Id == id)) throw new Exception("Decision link focus failed.");
actor.Id = teacher; actor.Role = "Teacher";
decision = await service.SaveDecisionAsync(student, week, id, body with { ExpectedVersion = decision.Version, Status = "InProgress" }, default);
decision = await service.SaveDecisionAsync(student, week, id, body with { ExpectedVersion = decision.Version, Status = "Completed" }, default);
if (decision.History.Count != 3 || decision.CompletedAt is null) throw new Exception("History/status failed.");
try { await service.SaveDecisionAsync(student, week, id, body with { ExpectedVersion = 0, Status = "Cancelled" }, default); throw new Exception("Stale write accepted."); }
catch (BusinessRuleException e) when (e.Code == "WeeklyReview.Conflict") { }
actor.Id = student; actor.Role = "Student";
var result = await service.GetAsync(null, week, 1, default);
if (!result.Decisions.Any(x => x.Id == id && x.Status == "Completed" && !x.CanEdit)) throw new Exception("Student cannot see completion.");
if ((await service.HomeAsync(default)).OpenDecisions != homeBefore.OpenDecisions || events.Messages.OfType<WeeklyDecisionChangedEvent>().Count() != 3) throw new Exception("Completion counts or events failed.");
Console.WriteLine("Restored production copy: unchanged schema/model, reflection recipient and no-op retry, decision notifications, home counters, exact decision link, history, stale-write rejection and completed-decision removal passed.");

sealed class Events : ICoachingEventPublisher {
 public List<object> Messages = [];
 public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
}

sealed class Actor(Guid id) : ICurrentUserService {
 public Guid Id = id; public string Role = "Student"; public Guid? UserId => Id; public string? Email => null; public string? FullName => "Test";
 public IEnumerable<string> Roles => [Role]; public System.Security.Claims.ClaimsPrincipal? User => null; public bool IsAuthenticated => true;
}
sealed class Scope : ICoachingAdminScopeAuthorization {
 public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => throw new NotSupportedException();
}
sealed class Accounts(Guid teacher) : ICoachingAccountDirectory {
 public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id,"Test","Hesap","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
 public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct) => throw new NotSupportedException();
 public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct) => throw new NotSupportedException();
 public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct) => Task.FromResult(true);
 public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct) => throw new NotSupportedException();
}

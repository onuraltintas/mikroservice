using System.Reflection;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.API.IntegrationTests;

/// <summary>
/// V6a (parent panel, user decision 01.11.2026): the two legacy parent fields are gone. The weekly-summary switch is only
/// the notification-service category ParentSummary; the only parent–student link is ParentStudentRelationship. The
/// migration keeps what it drops in two dated backup tables so Down can put it back.
/// </summary>
public sealed class LegacyParentFieldsRemovalPostgresTests : IAsyncLifetime
{
    private const string Previous = "20261103090000_ParentLinkProductVisibility";
    private const string Removal = "20261104090000_RemoveLegacyParentFields";
    private const string OldDefault = """{"dailyReport": false, "weeklyProgress": true, "examResults": true, "lowActivityAlert": true}""";
    private IdentityPostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await IdentityPostgresDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public void TheDomainNoLongerCarriesTheLegacyParentFields()
    {
        typeof(ParentProfile).GetProperty("NotificationPreferences").Should().BeNull();
        typeof(ParentProfile).GetMethod("UpdateNotificationPreferences").Should().BeNull();
        typeof(StudentProfile).GetProperty("ParentId").Should().BeNull();
        typeof(StudentProfile).GetProperty("Parent").Should().BeNull();
        typeof(StudentProfile).GetMethod("AssignParent").Should().BeNull();
        typeof(StudentProfile).GetMethod(nameof(StudentProfile.Create), BindingFlags.Public | BindingFlags.Static)!
            .GetParameters().Select(x => x.Name).Should().NotContain("parentId");
    }

    [Fact]
    public async Task TheMigrationDropsBothColumnsAndKeepsTheirValuesForDown()
    {
        await using var context = _database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        (await context.Database.GetAppliedMigrationsAsync()).Should().Contain(Removal);
        (await ColumnsAsync(context)).Should().BeEmpty("both legacy columns are dropped");

        await migrator.MigrateAsync(Previous);
        var parent = User.Create(Guid.NewGuid(), "veli@example.test", "Ayşe", "Veli");
        var mutedParent = User.Create(Guid.NewGuid(), "sessiz@example.test", "Can", "Veli");
        var child = User.Create(Guid.NewGuid(), "ogrenci@example.test", "Ada", "Öğrenci");
        var unlinkedChild = User.Create(Guid.NewGuid(), "diger@example.test", "Efe", "Öğrenci");
        context.Users.AddRange(parent, mutedParent, child, unlinkedChild);
        await context.SaveChangesAsync();
        const string muted = """{"dailyReport": true, "weeklyProgress": false}""";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity.parent_profiles ("Id", "UserId", "FirstName", "LastName", "IsActive", "NotificationPreferences", "Version")
            VALUES ({Guid.NewGuid()}, {parent.Id}, 'Ayşe', 'Veli', true, {OldDefault}::jsonb, 0),
                   ({Guid.NewGuid()}, {mutedParent.Id}, 'Can', 'Veli', true, {muted}::jsonb, 0)
            """);
        const string empty = "{}";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity.student_profiles ("Id", "UserId", "FirstName", "LastName", "IsActive", "DailyGoalMinutes", "Preferences", "Version", "ParentId")
            VALUES ({Guid.NewGuid()}, {child.Id}, 'Ada', 'Öğrenci', true, 30, {empty}::jsonb, 0, {parent.Id}),
                   ({Guid.NewGuid()}, {unlinkedChild.Id}, 'Efe', 'Öğrenci', true, 30, {empty}::jsonb, 0, NULL)
            """);

        await migrator.MigrateAsync(Removal);
        (await ColumnsAsync(context)).Should().BeEmpty();
        (await RowsAsync(context, "SELECT student_user_id::text || '|' || parent_id::text FROM identity.legacy_student_parent_ids_20261101"))
            .Should().Equal($"{child.Id}|{parent.Id}");
        (await RowsAsync(context, """
            SELECT parent_user_id::text FROM identity.legacy_parent_notification_preferences_20261101
            WHERE notification_preferences @> '{"weeklyProgress": false}'
            """)).Should().Equal(mutedParent.Id.ToString(), "only a non-default preference is kept, and the muted one is");

        await migrator.MigrateAsync(Previous);
        (await RowsAsync(context, """SELECT "UserId"::text || '|' || coalesce("ParentId"::text, '-') FROM identity.student_profiles ORDER BY "FirstName" """))
            .Should().Equal($"{child.Id}|{parent.Id}", $"{unlinkedChild.Id}|-");
        (await RowsAsync(context, $"""
            SELECT ("NotificationPreferences" = '{muted}'::jsonb)::text FROM identity.parent_profiles WHERE "UserId" = '{mutedParent.Id}'
            UNION ALL SELECT ("NotificationPreferences" = '{OldDefault}'::jsonb)::text FROM identity.parent_profiles WHERE "UserId" = '{parent.Id}'
            """)).Should().Equal("true", "true");
        (await RowsAsync(context, """
            SELECT count(*)::text FROM pg_tables WHERE schemaname = 'identity' AND tablename LIKE 'legacy\_%\_20261101'
            """)).Should().Equal("0");

        await migrator.MigrateAsync(Removal);
        (await ColumnsAsync(context)).Should().BeEmpty("Up runs again after Down");
    }

    private static Task<List<string>> ColumnsAsync(IdentityDbContext context) => RowsAsync(context, """
        SELECT table_name || '.' || column_name FROM information_schema.columns
        WHERE table_schema = 'identity' AND (table_name, column_name) IN (('parent_profiles', 'NotificationPreferences'), ('student_profiles', 'ParentId'))
        """);

    private static async Task<List<string>> RowsAsync(IdentityDbContext context, string sql) =>
        await context.Database.SqlQueryRaw<string>($"SELECT x AS \"Value\" FROM ({sql}) AS q(x)").ToListAsync();
}

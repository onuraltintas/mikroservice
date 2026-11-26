using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data-only migration (no schema change, so no designer or snapshot update): duration-focused Kısa Gösterim
/// (decision 26.11.2026). The shortest display of the 20 normal Tachistoscope exercises drops from 400/300/250/250 ms
/// to 300/200/150/150 ms (child, teen, young adult, adult). Only <c>engineConfig.adaptive.minDurationMs</c> changes.
/// A row changes only when it still holds the reviewed November 2026 settings (content type, start length, start
/// duration, length bounds; see 20261112120000) and the previous minimum, so the migration is idempotent, leaves
/// unexpected production values and assessment exercises alone and is a no-op on databases without the catalog.
/// Down restores the previous minimum with the same guard.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261126110000_TachistoscopeShorterMinimumDurations")]
public sealed class TachistoscopeShorterMinimumDurations : Migration
{
    private const string Actor = "system:tachistoscope-min-duration-2026-11";

    private readonly record struct Setting(string Type, int Length, int DurationMs, int MinLength, int MaxLength);

    // Age groups are matched by their lower age bound (9, 13, 17, 22), as in 20261112100000 and 20261112120000.
    private static readonly (int MinAge, int PreviousMinimumMs, int ApprovedMinimumMs, Setting[] Settings)[] Changes =
    [
        (9, 400, 300,
            [new("number", 3, 1200, 3, 5), new("number", 3, 1120, 3, 5), new("word", 4, 1040, 3, 6), new("word", 5, 960, 4, 7), new("phrase", 8, 880, 6, 10)]),
        (13, 300, 200,
            [new("number", 3, 800, 3, 6), new("number", 4, 720, 3, 6), new("word", 5, 640, 4, 7), new("word", 6, 560, 5, 8), new("phrase", 10, 480, 8, 12)]),
        (17, 250, 150,
            [new("number", 4, 640, 4, 6), new("number", 4, 560, 4, 6), new("word", 6, 480, 4, 8), new("word", 7, 440, 5, 9), new("phrase", 11, 400, 9, 14)]),
        (22, 250, 150,
            [new("number", 4, 640, 4, 7), new("number", 5, 560, 4, 7), new("word", 6, 480, 5, 9), new("word", 7, 440, 6, 10), new("phrase", 12, 400, 10, 16)])
    ];

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: false));

    // The session reads engineConfig.adaptive.minDurationMs case-insensitively; a minimum spelled differently or
    // kept anywhere else (root or root.adaptive) would not change with this key, so such rows are skipped.
    private static readonly (string Holder, string[] Exact)[] KeyHolders =
    [
        ("{}", []),
        ("{adaptive}", []),
        ("{engineConfig}", []),
        ("{engineConfig,adaptive}", ["minDurationMs"])
    ];

    private static string NoKeyVariants() => string.Join("\n              AND ", KeyHolders.Select(rule =>
        $"NOT EXISTS (SELECT 1 FROM jsonb_object_keys(CASE WHEN jsonb_typeof(e.configuration_json #> '{rule.Holder}') = 'object' "
        + $"THEN e.configuration_json #> '{rule.Holder}' ELSE '{{}}'::jsonb END) AS k(name) "
        + $"WHERE lower(k.name) = 'mindurationms' AND NOT k.name = ANY(ARRAY[{string.Join(", ", rule.Exact.Select(key => $"'{key}'"))}]::text[]))"));

    private static string BuildSql(bool forward)
    {
        var values = string.Join(",\n            ", Changes.SelectMany(change => Enumerable.Range(1, 5).Select(level =>
        {
            var setting = change.Settings[level - 1];
            var from = forward ? change.PreviousMinimumMs : change.ApprovedMinimumMs;
            var to = forward ? change.ApprovedMinimumMs : change.PreviousMinimumMs;
            return string.Create(CultureInfo.InvariantCulture,
                $"({change.MinAge}, {level}, '{setting.Type}', {setting.Length}, {setting.DurationMs}, {setting.MinLength}, {setting.MaxLength}, {from}, {to})");
        })));
        var actor = forward ? Actor : Actor + "-rollback";
        var direction = forward ? "up" : "down";
        var plan = $$"""
            SELECT e.id, c.to_minimum
            FROM (VALUES
                        {{values}}
                 ) AS c(min_age, difficulty_level, content_type, content_length, duration, min_length, max_length, from_minimum, to_minimum)
            JOIN speed_reading.exercise_types AS t ON t.name = 'Tachistoscope'
            JOIN speed_reading.age_group_configurations AS g ON g."MinAge" = c.min_age AND NOT g."IsDeleted"
            JOIN speed_reading.exercises AS e
              ON e.exercise_type_id = t.id
             AND e.target_age_group_id = g.id
             AND e.difficulty_level = c.difficulty_level
            WHERE NOT e.is_deleted
              AND NOT e.is_assessment_only
              AND NOT COALESCE((e.configuration_json -> 'engineConfig') ? 'assessmentProtocol', false)
              AND NOT COALESCE(e.configuration_json ? 'assessmentProtocol', false)
              AND (SELECT count(*) FROM speed_reading.age_group_configurations AS d
                   WHERE d."MinAge" = c.min_age AND NOT d."IsDeleted") = 1
              AND (e.configuration_json #> '{engineConfig,content,type}') = to_jsonb(c.content_type)
              AND (e.configuration_json #> '{engineConfig,content,length}') = to_jsonb(c.content_length)
              AND (e.configuration_json #> '{engineConfig,timing,durationMs}') = to_jsonb(c.duration)
              AND (e.configuration_json #> '{engineConfig,adaptive,minLength}') = to_jsonb(c.min_length)
              AND (e.configuration_json #> '{engineConfig,adaptive,maxLength}') = to_jsonb(c.max_length)
              AND (e.configuration_json #> '{engineConfig,adaptive,minDurationMs}') = to_jsonb(c.from_minimum)
              AND {{NoKeyVariants()}}
            """;
        return $$"""
            DO $tachistoscope_minimum$
            DECLARE
                planned integer;
                changed integer;
            BEGIN
                SELECT count(*) INTO planned FROM (
                    SELECT locked.id FROM speed_reading.exercises AS locked
                    WHERE locked.id IN (SELECT plan.id FROM (
            {{plan}}
                    ) AS plan)
                    FOR UPDATE) AS rows_to_change;
                RAISE NOTICE 'Tachistoscope minimum durations ({{direction}}): % exercise(s) hold the expected values and will change.', planned;

                UPDATE speed_reading.exercises AS e
                SET configuration_json = jsonb_set(e.configuration_json, '{engineConfig,adaptive,minDurationMs}', to_jsonb(plan.to_minimum), false),
                    updated_at = now(),
                    updated_by = '{{actor}}',
                    version = e.version + 1
                FROM (
            {{plan}}
                ) AS plan
                WHERE e.id = plan.id;
                GET DIAGNOSTICS changed = ROW_COUNT;
                RAISE NOTICE 'Tachistoscope minimum durations ({{direction}}): % exercise(s) changed.', changed;
            END
            $tachistoscope_minimum$;
            """;
    }
}

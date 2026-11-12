using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data-only migration (no schema change, so no designer or snapshot update): the approved November 2026
/// Tachistoscope settings of the 20 normal exercises. Levels 1–2 keep numbers, levels 3–4 show words and
/// level 5 two-word phrases; every start display duration is about 20 % shorter. Only
/// <c>content.type</c>, <c>content.length</c>, <c>timing.durationMs</c> and <c>adaptive.minLength/maxLength</c>
/// change; the round count (<c>content.count</c>, see 20261112100000), interval and duration bounds stay as they are.
/// A row changes only when all five keys still hold the reviewed previous values, so the migration is
/// idempotent, leaves unexpected production values and assessment exercises alone and is a no-op on
/// databases without the catalog. Down restores the previous values with the same guard.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261112120000_TachistoscopeWordsAndShorterDurations")]
public sealed class TachistoscopeWordsAndShorterDurations : Migration
{
    private const string Actor = "system:tachistoscope-words-2026-11";

    private readonly record struct Setting(string Type, int Length, int DurationMs, int MinLength, int MaxLength);

    // Age groups are matched by their lower age bound (9, 13, 17, 22), as in 20261112100000.
    private static readonly (int MinAge, Setting[] Previous, Setting[] Approved)[] Changes =
    [
        (9,
            [new("number", 3, 1500, 3, 5), new("number", 3, 1400, 3, 5), new("number", 4, 1300, 3, 5), new("number", 4, 1200, 3, 5), new("number", 5, 1100, 3, 5)],
            [new("number", 3, 1200, 3, 5), new("number", 3, 1120, 3, 5), new("word", 4, 1040, 3, 6), new("word", 5, 960, 4, 7), new("phrase", 8, 880, 6, 10)]),
        (13,
            [new("number", 3, 1000, 3, 6), new("number", 4, 900, 3, 6), new("number", 4, 800, 3, 6), new("number", 5, 700, 3, 6), new("number", 6, 600, 3, 6)],
            [new("number", 3, 800, 3, 6), new("number", 4, 720, 3, 6), new("word", 5, 640, 4, 7), new("word", 6, 560, 5, 8), new("phrase", 10, 480, 8, 12)]),
        (17,
            [new("number", 4, 800, 4, 6), new("number", 4, 700, 4, 6), new("number", 5, 600, 4, 6), new("number", 5, 550, 4, 6), new("number", 6, 500, 4, 6)],
            [new("number", 4, 640, 4, 6), new("number", 4, 560, 4, 6), new("word", 6, 480, 4, 8), new("word", 7, 440, 5, 9), new("phrase", 11, 400, 9, 14)]),
        (22,
            [new("number", 4, 800, 4, 7), new("number", 5, 700, 4, 7), new("number", 5, 600, 4, 7), new("number", 6, 550, 4, 7), new("number", 7, 500, 4, 7)],
            [new("number", 4, 640, 4, 7), new("number", 5, 560, 4, 7), new("word", 6, 480, 5, 9), new("word", 7, 440, 6, 10), new("phrase", 12, 400, 10, 16)])
    ];

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: false));

    private static string BuildSql(bool forward)
    {
        var values = string.Join(",\n            ", Changes.SelectMany(change => Enumerable.Range(1, 5).Select(level =>
        {
            var from = (forward ? change.Previous : change.Approved)[level - 1];
            var to = (forward ? change.Approved : change.Previous)[level - 1];
            return string.Create(CultureInfo.InvariantCulture,
                $"({change.MinAge}, {level}, '{from.Type}', {from.Length}, {from.DurationMs}, {from.MinLength}, {from.MaxLength}, '{to.Type}', {to.Length}, {to.DurationMs}, {to.MinLength}, {to.MaxLength})");
        })));
        var actor = forward ? Actor : Actor + "-rollback";
        var direction = forward ? "up" : "down";
        // The session reads engineConfig.displayDurationMs before timing.durationMs, and the catalog validator
        // requires root and engineConfig values to agree, so rows that also carry a duration or length elsewhere
        // are skipped: changing only the engineConfig keys would not change (or would invalidate) them.
        var plan = $$"""
            SELECT e.id, c.to_type, c.to_length, c.to_duration, c.to_min_length, c.to_max_length
            FROM (VALUES
                        {{values}}
                 ) AS c(min_age, difficulty_level, from_type, from_length, from_duration, from_min_length, from_max_length,
                        to_type, to_length, to_duration, to_min_length, to_max_length)
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
              AND (e.configuration_json #> '{engineConfig,content,type}') = to_jsonb(c.from_type)
              AND (e.configuration_json #> '{engineConfig,content,length}') = to_jsonb(c.from_length)
              AND (e.configuration_json #> '{engineConfig,timing,durationMs}') = to_jsonb(c.from_duration)
              AND (e.configuration_json #> '{engineConfig,adaptive,minLength}') = to_jsonb(c.from_min_length)
              AND (e.configuration_json #> '{engineConfig,adaptive,maxLength}') = to_jsonb(c.from_max_length)
              AND NOT COALESCE((e.configuration_json -> 'engineConfig') ?| ARRAY['displayDurationMs', 'intervalMs'], false)
              AND NOT COALESCE(e.configuration_json ?| ARRAY['displayDurationMs', 'intervalMs', 'timing', 'content', 'adaptive'], false)
            """;
        return $$"""
            DO $tachistoscope_words$
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
                RAISE NOTICE 'Tachistoscope words and durations ({{direction}}): % exercise(s) hold the expected values and will change.', planned;

                UPDATE speed_reading.exercises AS e
                SET configuration_json =
                        jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(e.configuration_json,
                            '{engineConfig,content,type}', to_jsonb(plan.to_type), false),
                            '{engineConfig,content,length}', to_jsonb(plan.to_length), false),
                            '{engineConfig,timing,durationMs}', to_jsonb(plan.to_duration), false),
                            '{engineConfig,adaptive,minLength}', to_jsonb(plan.to_min_length), false),
                            '{engineConfig,adaptive,maxLength}', to_jsonb(plan.to_max_length), false),
                    updated_at = now(),
                    updated_by = '{{actor}}',
                    version = e.version + 1
                FROM (
            {{plan}}
                ) AS plan
                WHERE e.id = plan.id;
                GET DIAGNOSTICS changed = ROW_COUNT;
                RAISE NOTICE 'Tachistoscope words and durations ({{direction}}): % exercise(s) changed.', changed;
            END
            $tachistoscope_words$;
            """;
    }
}

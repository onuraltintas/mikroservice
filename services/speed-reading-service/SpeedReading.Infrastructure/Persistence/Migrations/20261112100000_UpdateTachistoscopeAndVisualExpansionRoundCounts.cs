using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data-only migration (no schema change, so no designer or snapshot update): sets the approved
/// November 2026 round counts of the normal Tachistoscope and VisualExpansion exercises.
/// A row changes only when it still holds the reviewed previous value, so the migration is
/// idempotent, leaves unexpected production values and assessment exercises alone and is a no-op
/// on databases without the catalog. Down restores the previous values with the same guard.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261112100000_UpdateTachistoscopeAndVisualExpansionRoundCounts")]
public sealed class UpdateTachistoscopeAndVisualExpansionRoundCounts : Migration
{
    private const string Actor = "system:round-counts-2026-11";

    // Age groups are matched by their lower age bound (9, 13, 17, 22): their ids are not seeded in code,
    // the open-ended adult group may store its upper bound as NULL, and OrderIndex is only a display order.
    private static readonly (string Type, int MinAge, int[] Previous, int[] Approved)[] Changes =
    [
        ("Tachistoscope", 9, [20, 25, 30, 35, 40], [15, 19, 23, 26, 30]),
        ("Tachistoscope", 13, [25, 30, 35, 40, 45], [20, 24, 28, 31, 35]),
        ("Tachistoscope", 17, [30, 35, 40, 45, 50], [20, 24, 28, 31, 35]),
        ("Tachistoscope", 22, [30, 35, 40, 45, 50], [20, 25, 30, 35, 40]),
        ("VisualExpansion", 9, [30, 35, 40, 45, 50], [15, 19, 23, 26, 30]),
        ("VisualExpansion", 13, [35, 40, 45, 50, 55], [20, 24, 28, 31, 35]),
        ("VisualExpansion", 17, [40, 45, 50, 55, 60], [20, 24, 28, 31, 35]),
        ("VisualExpansion", 22, [45, 50, 55, 60, 65], [20, 25, 30, 35, 40])
    ];

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: false));

    private static string BuildSql(bool forward)
    {
        var values = string.Join(",\n            ", Changes.SelectMany(change => Enumerable.Range(1, 5).Select(level =>
            string.Create(CultureInfo.InvariantCulture,
                $"('{change.Type}', {change.MinAge}, {level}, {(forward ? change.Previous : change.Approved)[level - 1]}, {(forward ? change.Approved : change.Previous)[level - 1]})"))));
        var actor = forward ? Actor : Actor + "-rollback";
        var direction = forward ? "up" : "down";
        // Tachistoscope: TotalSteps = engineConfig.totalStimuli ?? engineConfig.content.count.
        // VisualExpansion: TotalSteps = engineConfig.(totalSteps|itemCount|rounds) ?? root (totalSteps|itemCount|rounds).
        // Rows carrying an overriding key are skipped because changing the round key would not change their sessions.
        var plan = $$"""
            SELECT e.id, c.type_name, c.to_rounds
            FROM (VALUES
                        {{values}}
                 ) AS c(type_name, min_age, difficulty_level, from_rounds, to_rounds)
            JOIN speed_reading.exercise_types AS t ON t.name = c.type_name
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
              AND CASE c.type_name
                  WHEN 'Tachistoscope' THEN
                      (e.configuration_json #> '{engineConfig,content,count}') = to_jsonb(c.from_rounds)
                      AND NOT COALESCE((e.configuration_json -> 'engineConfig') ?| ARRAY['totalStimuli', 'totalWords'], false)
                      AND NOT COALESCE(e.configuration_json ?| ARRAY['totalStimuli', 'totalWords'], false)
                  ELSE
                      COALESCE(e.configuration_json #> '{engineConfig,rounds}', e.configuration_json -> 'rounds') = to_jsonb(c.from_rounds)
                      AND COALESCE(e.configuration_json -> 'rounds', to_jsonb(c.from_rounds)) = to_jsonb(c.from_rounds)
                      AND NOT COALESCE((e.configuration_json -> 'engineConfig') ?| ARRAY['totalSteps', 'itemCount'], false)
                      AND NOT COALESCE(e.configuration_json ?| ARRAY['totalSteps', 'itemCount'], false)
                  END
            """;
        return $$"""
            DO $round_counts$
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
                RAISE NOTICE 'Speed-reading round counts ({{direction}}): % exercise(s) hold the expected value and will change.', planned;

                UPDATE speed_reading.exercises AS e
                SET configuration_json = CASE plan.type_name
                        WHEN 'Tachistoscope' THEN
                            jsonb_set(e.configuration_json, '{engineConfig,content,count}', to_jsonb(plan.to_rounds), false)
                        ELSE (
                            SELECT CASE WHEN step.config #> '{engineConfig,rounds}' IS NOT NULL
                                        THEN jsonb_set(step.config, '{engineConfig,rounds}', to_jsonb(plan.to_rounds), false)
                                        ELSE step.config END
                            FROM (SELECT CASE WHEN e.configuration_json ? 'rounds'
                                              THEN jsonb_set(e.configuration_json, '{rounds}', to_jsonb(plan.to_rounds), false)
                                              ELSE e.configuration_json END AS config) AS step)
                    END,
                    updated_at = now(),
                    updated_by = '{{actor}}',
                    version = e.version + 1
                FROM (
            {{plan}}
                ) AS plan
                WHERE e.id = plan.id;
                GET DIAGNOSTICS changed = ROW_COUNT;
                RAISE NOTICE 'Speed-reading round counts ({{direction}}): % exercise(s) changed.', changed;
            END
            $round_counts$;
            """;
    }
}

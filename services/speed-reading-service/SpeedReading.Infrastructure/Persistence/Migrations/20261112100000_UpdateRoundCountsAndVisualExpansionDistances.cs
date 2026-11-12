using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data-only migration (no schema change, so no designer or snapshot update) with the approved November 2026
/// settings: the round counts of the normal Tachistoscope and VisualExpansion exercises and the start/target
/// distances of the normal VisualExpansion exercises. Rounds and distances are guarded independently: each
/// changes only while every location of it still holds the reviewed previous value, so the migration is
/// idempotent, leaves unexpected production values and assessment exercises alone and is a no-op on databases
/// without the catalog. A row changed by either part gets one audit/version bump. Down restores the previous
/// values with the same guard.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261112100000_UpdateRoundCountsAndVisualExpansionDistances")]
public sealed class UpdateRoundCountsAndVisualExpansionDistances : Migration
{
    private const string Actor = "system:rounds-distances-2026-11";

    // Age groups are matched by their lower age bound (9, 13, 17, 22): their ids are not seeded in code,
    // the open-ended adult group may store its upper bound as NULL, and OrderIndex is only a display order.
    private static readonly (int MinAge, int[] PreviousTachistoscope, int[] PreviousVisual, int[] ApprovedRounds)[] Rounds =
    [
        (9, [20, 25, 30, 35, 40], [30, 35, 40, 45, 50], [15, 19, 23, 26, 30]),
        (13, [25, 30, 35, 40, 45], [35, 40, 45, 50, 55], [20, 24, 28, 31, 35]),
        (17, [30, 35, 40, 45, 50], [40, 45, 50, 55, 60], [20, 24, 28, 31, 35]),
        (22, [30, 35, 40, 45, 50], [45, 50, 55, 60, 65], [20, 25, 30, 35, 40])
    ];

    private const int PreviousStart = 8;

    private static readonly (int MinAge, int[] PreviousTarget, int[] ApprovedStart, int[] ApprovedTarget)[] Distances =
    [
        (9, [16, 18, 20, 22, 24], [11, 14, 17, 20, 23], [21, 27, 33, 38, 44]),
        (13, [18, 20, 22, 24, 26], [11, 15, 19, 23, 27], [25, 32, 39, 45, 52]),
        (17, [18, 20, 22, 24, 26], [11, 15, 19, 23, 27], [25, 32, 39, 45, 52]),
        (22, [18, 20, 22, 24, 26], [11, 15, 19, 23, 27], [25, 33, 40, 48, 55])
    ];

    // Every location the server (OwnedSpeedReadingExerciseSessions) or the player reads the setting from.
    private static readonly string[] TachistoscopeRoundPaths = ["{engineConfig,content,count}"];
    private static readonly string[] VisualRoundPaths = ["{engineConfig,rounds}", "{rounds}"];
    private static readonly string[] StartPaths =
        ["{engineConfig,expansion,startDegrees}", "{engineConfig,startDegrees}", "{expansion,startDegrees}", "{startDegrees}"];
    private static readonly string[] TargetPaths =
        ["{engineConfig,expansion,targetDegrees}", "{engineConfig,targetDegrees}", "{expansion,targetDegrees}", "{targetDegrees}"];

    // The runtime matches keys case-insensitively (ReadProperty/ReadObject/ReadPositiveInt), so a setting is skipped
    // when any object it is read from holds a key whose lower-case form is listed but which is not the exact
    // camelCase key the migration writes (for example "EngineConfig", "Expansion", "StartDegrees"), or holds an
    // override in any case (totalStimuli/totalWords, totalSteps/itemCount) or a player-read distance alias.
    private static readonly (string[] Holders, string[] Keys, string[] Exact) TachistoscopeRoundKeys = (
        ["{}", "{engineConfig}", "{engineConfig,content}"],
        ["engineConfig", "content", "count", "totalStimuli", "totalWords"],
        ["engineConfig", "content", "count"]);
    private static readonly (string[] Holders, string[] Keys, string[] Exact) VisualRoundKeys = (
        ["{}", "{engineConfig}"],
        ["engineConfig", "rounds", "totalSteps", "itemCount"],
        ["engineConfig", "rounds"]);
    private static readonly (string[] Holders, string[] Keys, string[] Exact) DistanceKeys = (
        ["{}", "{engineConfig}", "{expansion}", "{engineConfig,expansion}"],
        ["engineConfig", "expansion", "startDegrees", "targetDegrees",
            "visualExpansionStartDegrees", "visualExpansionTargetDegrees", "visualExpansionCurrentDegrees"],
        ["engineConfig", "expansion", "startDegrees", "targetDegrees"]);

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BuildSql(forward: false));

    private static string BuildSql(bool forward)
    {
        static string Pair(int previous, int approved, bool forward) =>
            forward ? $"{previous}, {approved}" : $"{approved}, {previous}";
        var rows = new List<string>();
        foreach (var (minAge, tachistoscope, visual, approved) in Rounds)
        {
            var distances = Distances.Single(item => item.MinAge == minAge);
            for (var index = 0; index < 5; index++)
            {
                rows.Add(string.Create(CultureInfo.InvariantCulture,
                    $"('Tachistoscope', {minAge}, {index + 1}, {Pair(tachistoscope[index], approved[index], forward)}, NULL::integer, NULL::integer, NULL::integer, NULL::integer)"));
                rows.Add(string.Create(CultureInfo.InvariantCulture,
                    $"('VisualExpansion', {minAge}, {index + 1}, {Pair(visual[index], approved[index], forward)}, "
                    + $"{Pair(PreviousStart, distances.ApprovedStart[index], forward)}, {Pair(distances.PreviousTarget[index], distances.ApprovedTarget[index], forward)})"));
            }
        }

        var roundsGuard = $"""
            CASE x.type_name
                WHEN 'Tachistoscope' THEN {AllEqual(TachistoscopeRoundPaths, "x.from_rounds")}
                    AND {NoKeyVariants(TachistoscopeRoundKeys)}
                ELSE {AllEqual(VisualRoundPaths, "x.from_rounds")}
                    AND {NoKeyVariants(VisualRoundKeys)}
            END
            """;
        var distanceGuard = $"""
            x.type_name = 'VisualExpansion'
                AND {AllEqual(StartPaths, "x.from_start")}
                AND {AllEqual(TargetPaths, "x.from_target")}
                AND {NoKeyVariants(DistanceKeys)}
            """;
        static string Paths(string[] paths) => "ARRAY[" + string.Join(", ", paths.Select(path => $"'{path}'")) + "]";

        var plan = $$"""
            SELECT p.id, p.cfg, p.type_name, p.rounds_to, p.start_to, p.target_to
            FROM (
                SELECT x.id, x.type_name, x.cfg,
                       CASE WHEN {{roundsGuard}} THEN x.to_rounds END AS rounds_to,
                       CASE WHEN {{distanceGuard}} THEN x.to_start END AS start_to,
                       CASE WHEN {{distanceGuard}} THEN x.to_target END AS target_to
                FROM (
                    SELECT e.id, e.configuration_json AS cfg, c.*
                    FROM (VALUES
                        {{string.Join(",\n            ", rows)}}
                    ) AS c(type_name, min_age, difficulty_level, from_rounds, to_rounds, from_start, to_start, from_target, to_target)
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
                ) AS x
            ) AS p
            WHERE p.rounds_to IS NOT NULL OR p.start_to IS NOT NULL
            """;
        var actor = forward ? Actor : Actor + "-rollback";
        var direction = forward ? "up" : "down";
        // A row is written only if it still holds the configuration the plan read (compare-and-set), so a
        // concurrent catalog edit is never overwritten; each changed row gets exactly one audit/version bump.
        return $$"""
            DO $rounds_distances$
            DECLARE
                rounds_planned integer;
                distances_planned integer;
                changed integer := 0;
                r record;
                updated jsonb;
                path text;
            BEGIN
                SELECT count(*) FILTER (WHERE plan.rounds_to IS NOT NULL), count(*) FILTER (WHERE plan.start_to IS NOT NULL)
                INTO rounds_planned, distances_planned
                FROM (
            {{plan}}
                ) AS plan;
                RAISE NOTICE 'Speed-reading settings ({{direction}}): % exercise(s) will get new round counts, % Görsel Genişleme exercise(s) new distances.', rounds_planned, distances_planned;

                FOR r IN
            {{plan}}
                LOOP
                    updated := r.cfg;
                    IF r.rounds_to IS NOT NULL THEN
                        FOREACH path IN ARRAY CASE WHEN r.type_name = 'Tachistoscope'
                            THEN {{Paths(TachistoscopeRoundPaths)}} ELSE {{Paths(VisualRoundPaths)}} END
                        LOOP
                            IF updated #> path::text[] IS NOT NULL THEN
                                updated := jsonb_set(updated, path::text[], to_jsonb(r.rounds_to), false);
                            END IF;
                        END LOOP;
                    END IF;
                    IF r.start_to IS NOT NULL THEN
                        FOREACH path IN ARRAY {{Paths(StartPaths)}} LOOP
                            IF updated #> path::text[] IS NOT NULL THEN
                                updated := jsonb_set(updated, path::text[], to_jsonb(r.start_to), false);
                            END IF;
                        END LOOP;
                        FOREACH path IN ARRAY {{Paths(TargetPaths)}} LOOP
                            IF updated #> path::text[] IS NOT NULL THEN
                                updated := jsonb_set(updated, path::text[], to_jsonb(r.target_to), false);
                            END IF;
                        END LOOP;
                    END IF;
                    UPDATE speed_reading.exercises
                    SET configuration_json = updated,
                        updated_at = now(),
                        updated_by = '{{actor}}',
                        version = version + 1
                    WHERE id = r.id AND configuration_json = r.cfg;
                    IF FOUND THEN changed := changed + 1; END IF;
                END LOOP;
                RAISE NOTICE 'Speed-reading settings ({{direction}}): % exercise(s) changed.', changed;
            END
            $rounds_distances$;
            """;
    }

    // Every location that is present holds the expected value, and at least one is present.
    private static string AllEqual(string[] paths, string expected) =>
        $"COALESCE({string.Join(", ", paths.Select(path => $"x.cfg #> '{path}'"))}) = to_jsonb({expected})"
        + string.Concat(paths.Select(path => $" AND COALESCE(x.cfg #> '{path}', to_jsonb({expected})) = to_jsonb({expected})"));

    private static string NoKeyVariants((string[] Holders, string[] Keys, string[] Exact) rule)
    {
        static string Array(IEnumerable<string> values) => "ARRAY[" + string.Join(", ", values.Select(value => $"'{value}'")) + "]::text[]";
        var keys = Array(rule.Keys.Select(key => key.ToLowerInvariant()));
        var exact = Array(rule.Exact);
        return string.Join("\n                    AND ", rule.Holders.Select(holder =>
            $"NOT EXISTS (SELECT 1 FROM jsonb_object_keys(CASE WHEN jsonb_typeof(x.cfg #> '{holder}') = 'object' "
            + $"THEN x.cfg #> '{holder}' ELSE '{{}}'::jsonb END) AS k(name) "
            + $"WHERE lower(k.name) = ANY({keys}) AND NOT k.name = ANY({exact}))"));
    }
}

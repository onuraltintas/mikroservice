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

    // Keys that override the round key, and distance aliases the player would prefer over the server's values.
    private const string TachistoscopeRoundOverrides = "ARRAY['totalStimuli', 'totalWords']";
    private const string VisualRoundOverrides = "ARRAY['totalSteps', 'itemCount']";
    private const string DistanceAliases =
        "ARRAY['VisualExpansionStartDegrees', 'visualExpansionStartDegrees', 'StartDegrees', "
        + "'VisualExpansionTargetDegrees', 'visualExpansionTargetDegrees', 'TargetDegrees', "
        + "'VisualExpansionCurrentDegrees', 'visualExpansionCurrentDegrees']";
    private static readonly string[] DistanceAliasHolders = ["{}", "{engineConfig}", "{expansion}", "{engineConfig,expansion}"];

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
                    AND {NoKeys(["{engineConfig}", "{}"], TachistoscopeRoundOverrides)}
                ELSE {AllEqual(VisualRoundPaths, "x.from_rounds")}
                    AND {NoKeys(["{engineConfig}", "{}"], VisualRoundOverrides)}
            END
            """;
        var distanceGuard = $"""
            x.type_name = 'VisualExpansion'
                AND {AllEqual(StartPaths, "x.from_start")}
                AND {AllEqual(TargetPaths, "x.from_target")}
                AND {NoKeys(DistanceAliasHolders, DistanceAliases)}
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

    private static string NoKeys(string[] holders, string keys) =>
        string.Join(" AND ", holders.Select(holder => $"NOT COALESCE((x.cfg #> '{holder}') ?| {keys}, false)"));
}

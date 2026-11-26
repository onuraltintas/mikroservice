using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data-only migration (no schema change, so no designer or snapshot update): Kelime Gruplama tempo and group size by
/// age and level (user decision 26.11.2026). Children get 2–3 words per group (2/100, 2/120, 2/140, 3/160, 3/180), teens
/// a lower tempo (2/130, 3/150, 3/180, 3/210, 4/240), young adults and adults at most 4 words at level 5. The word bands
/// cover the pack's short selections and every existing 4-question text of the age and level (live count 26.11.2026), so
/// the four-question rule never leaves a level without texts. The new configurations are exactly those of the regenerated
/// <c>content-packs/grouping/v1</c> pack, so the pack sees migrated rows as already applied.
/// A row changes only when it holds exactly the previous pack configuration or, before the pack was applied, exactly the
/// previous catalog configuration (levels 1–4); edited, deleted and assessment rows stay. The audit actor records which
/// source the row had, so Down restores exactly that configuration. Idempotent; a no-op without the catalog.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261126120000_GroupingTempoByAge")]
public sealed class GroupingTempoByAge : Migration
{
    private const string FromPack = "system:grouping-tempo-2026-11-pack";
    private const string FromCatalog = "system:grouping-tempo-2026-11-catalog";

    // id, age, level, previous catalog configuration (levels 1–4), previous pack configuration, new configuration.
    private const string Values = """
            ('c034a403-0215-51c1-8727-c6e690c5226f'::uuid, 'child', 1, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":100,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":150,"maxWordCount":200},"pacer":{"chunkSize":2,"speedWpm":100}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":160,"maxWordCount":460},"pacer":{"chunkSize":2,"speedWpm":100}}}'::jsonb),
            ('42296217-4ce6-5acd-88a3-fe636cbffb30'::uuid, 'child', 2, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":130,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":195,"maxWordCount":260},"pacer":{"chunkSize":3,"speedWpm":130}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":220,"maxWordCount":470},"pacer":{"chunkSize":2,"speedWpm":120}}}'::jsonb),
            ('3e0c4ca7-b16d-5700-8e28-79724f53dd94'::uuid, 'child', 3, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":160,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":3}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":240,"maxWordCount":320},"pacer":{"chunkSize":4,"speedWpm":160}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":260,"maxWordCount":400},"pacer":{"chunkSize":2,"speedWpm":140}}}'::jsonb),
            ('158e91aa-b6b4-50f9-afbc-752d59c9cd07'::uuid, 'child', 4, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":200,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":400},"pacer":{"chunkSize":4,"speedWpm":200}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":390},"pacer":{"chunkSize":3,"speedWpm":160}}}'::jsonb),
            ('d7b10000-0000-5000-8000-000000000001'::uuid, 'child', 5, NULL::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":345,"maxWordCount":460},"pacer":{"chunkSize":4,"speedWpm":230}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":400},"pacer":{"chunkSize":3,"speedWpm":180}}}'::jsonb),
            ('89bedb19-f023-5d7f-9a3f-9a986f9d33c2'::uuid, 'teen', 1, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":150,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":375},"pacer":{"chunkSize":2,"speedWpm":150}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":280,"maxWordCount":430},"pacer":{"chunkSize":2,"speedWpm":130}}}'::jsonb),
            ('4d2d13e4-92b0-5201-b847-5c7f1bd0edbb'::uuid, 'teen', 2, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":450},"pacer":{"chunkSize":3,"speedWpm":180}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":280,"maxWordCount":400},"pacer":{"chunkSize":3,"speedWpm":150}}}'::jsonb),
            ('bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f'::uuid, 'teen', 3, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":525},"pacer":{"chunkSize":3,"speedWpm":210}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":300,"maxWordCount":490},"pacer":{"chunkSize":3,"speedWpm":180}}}'::jsonb),
            ('c93c2dec-eb3b-50b6-890d-4c416037565d'::uuid, 'teen', 4, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":480,"maxWordCount":600},"pacer":{"chunkSize":4,"speedWpm":240}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":410,"maxWordCount":560},"pacer":{"chunkSize":3,"speedWpm":210}}}'::jsonb),
            ('d7b10000-0000-5000-8000-000000000002'::uuid, 'teen', 5, NULL::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":675},"pacer":{"chunkSize":4,"speedWpm":270}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":640},"pacer":{"chunkSize":4,"speedWpm":240}}}'::jsonb),
            ('617a2e9d-3aed-5afb-b20b-39bcbc2848ba'::uuid, 'young-adult', 1, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":540},"pacer":{"chunkSize":2,"speedWpm":180}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":450,"maxWordCount":770},"pacer":{"chunkSize":2,"speedWpm":180}}}'::jsonb),
            ('c53fae0b-d606-539d-b02a-60d080fc8e75'::uuid, 'young-adult', 2, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":630},"pacer":{"chunkSize":3,"speedWpm":210}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":510,"maxWordCount":730},"pacer":{"chunkSize":3,"speedWpm":210}}}'::jsonb),
            ('eb87713b-d508-5d54-8556-fe7f0765bd90'::uuid, 'young-adult', 3, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":480,"maxWordCount":720},"pacer":{"chunkSize":3,"speedWpm":240}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":580,"maxWordCount":730},"pacer":{"chunkSize":3,"speedWpm":240}}}'::jsonb),
            ('57d186e0-8756-5e69-aaa2-3dd457c931b9'::uuid, 'young-adult', 4, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":270,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":540,"maxWordCount":810},"pacer":{"chunkSize":4,"speedWpm":270}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":420,"maxWordCount":690},"pacer":{"chunkSize":4,"speedWpm":270}}}'::jsonb),
            ('d7b10000-0000-5000-8000-000000000004'::uuid, 'young-adult', 5, NULL::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":600,"maxWordCount":900},"pacer":{"chunkSize":5,"speedWpm":300}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":430,"maxWordCount":700},"pacer":{"chunkSize":4,"speedWpm":300}}}'::jsonb),
            ('02fc1e52-80d6-506e-9317-fd5eec33ea2d'::uuid, 'adult', 1, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":200,"chunkSize":2},"readingPurpose":"practice"},"difficultyLevel":1}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":400,"maxWordCount":600},"pacer":{"chunkSize":2,"speedWpm":200}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":1,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":360,"maxWordCount":590},"pacer":{"chunkSize":2,"speedWpm":200}}}'::jsonb),
            ('8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65'::uuid, 'adult', 2, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":230,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":2}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":460,"maxWordCount":690},"pacer":{"chunkSize":3,"speedWpm":230}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":2,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":310,"maxWordCount":590},"pacer":{"chunkSize":3,"speedWpm":230}}}'::jsonb),
            ('1c5d756b-2bef-58e8-99c2-1e32d32e8153'::uuid, 'adult', 3, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":260,"chunkSize":3},"readingPurpose":"practice"},"difficultyLevel":3}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":520,"maxWordCount":780},"pacer":{"chunkSize":3,"speedWpm":260}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":3,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":340,"maxWordCount":820},"pacer":{"chunkSize":3,"speedWpm":260}}}'::jsonb),
            ('e4900566-c2ba-5254-bde7-215bb4ae5ca7'::uuid, 'adult', 4, '{"engineType":"word_highlight","engineConfig":{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":290,"chunkSize":4},"readingPurpose":"practice"},"difficultyLevel":4}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":580,"maxWordCount":870},"pacer":{"chunkSize":4,"speedWpm":290}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":4,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":680,"maxWordCount":960},"pacer":{"chunkSize":4,"speedWpm":290}}}'::jsonb),
            ('d7b10000-0000-5000-8000-000000000003'::uuid, 'adult', 5, NULL::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":640,"maxWordCount":960},"pacer":{"chunkSize":5,"speedWpm":320}}}'::jsonb,
                '{"engineType":"word_highlight","difficultyLevel":5,"engineConfig":{"mode":"chunking","readingPurpose":"practice","content":{"source":"reading_text","minWordCount":470,"maxWordCount":1050},"pacer":{"chunkSize":4,"speedWpm":320}}}'::jsonb)
        """;

    private const string Plan = $$"""
        WITH c(id, age, level, catalog_config, pack_config, new_config) AS (VALUES
        {{Values}}
        )
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql($$"""
        DO $grouping_tempo$
        DECLARE changed integer;
        BEGIN
            {{Plan}}
            UPDATE speed_reading.exercises AS e
            SET configuration_json = c.new_config,
                updated_at = now(),
                updated_by = CASE WHEN e.configuration_json = c.pack_config THEN '{{FromPack}}' ELSE '{{FromCatalog}}' END,
                version = e.version + 1
            FROM c
            JOIN speed_reading.exercise_types AS t ON t.name = 'Chunking'
            WHERE e.id = c.id
              AND e.exercise_type_id = t.id
              AND e.difficulty_level = c.level
              AND NOT e.is_deleted
              AND NOT e.is_assessment_only
              AND (e.configuration_json = c.pack_config
                   OR (c.catalog_config IS NOT NULL AND e.configuration_json = c.catalog_config));
            GET DIAGNOSTICS changed = ROW_COUNT;
            RAISE NOTICE 'Kelime Gruplama tempo (up): % exercise(s) changed.', changed;
        END
        $grouping_tempo$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql($$"""
        DO $grouping_tempo$
        DECLARE changed integer;
        BEGIN
            {{Plan}}
            UPDATE speed_reading.exercises AS e
            SET configuration_json = CASE WHEN e.updated_by = '{{FromCatalog}}' THEN c.catalog_config ELSE c.pack_config END,
                updated_at = now(),
                updated_by = e.updated_by || '-rollback',
                version = e.version + 1
            FROM c
            WHERE e.id = c.id
              AND NOT e.is_deleted
              AND e.configuration_json = c.new_config
              AND (e.updated_by = '{{FromPack}}' OR (e.updated_by = '{{FromCatalog}}' AND c.catalog_config IS NOT NULL));
            GET DIAGNOSTICS changed = ROW_COUNT;
            RAISE NOTICE 'Kelime Gruplama tempo (down): % exercise(s) restored.', changed;
        END
        $grouping_tempo$;
        """);
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260923103000_CompleteAssignedProgramCatalog")]
public sealed class CompleteAssignedProgramCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            WITH variants(key, type_name, title, description, configuration) AS (
                VALUES
                ('chunk-pairs', 'Chunking', 'İkili Kelime Grupları', 'İki kelimelik grupları tek bakışta oku.', '{"engineType":"word_highlight","engineConfig":{"mode":"chunking","timing":{"delayMs":450,"durationMs":850},"content":{"source":"reading_text","chunkSize":2},"visuals":{"fontSize":"medium","highlightColor":"yellow"}},"difficultyLevel":1}'::jsonb),
                ('chunk-triples', 'Chunking', 'Üçlü Kelime Grupları', 'Üç kelimelik grupların anlamını birlikte kavra.', '{"engineType":"word_highlight","engineConfig":{"mode":"chunking","timing":{"delayMs":550,"durationMs":1000},"content":{"source":"reading_text","chunkSize":3},"visuals":{"fontSize":"medium","highlightColor":"blue"}},"difficultyLevel":1}'::jsonb),
                ('eye-circle', 'EyeTracking', 'Dairesel Göz Takibi', 'Gözlerinle dairesel hareketi takip et.', '{"engineType":"motion_path","engineConfig":{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":60,"speedMs":1250},"content":{"type":"dot","pointSize":36}},"difficultyLevel":1}'::jsonb),
                ('fixation-three', 'Fixation', 'Üç Nokta Sabitleme', 'Üç noktayı sırayla sabitleyerek algıla.', '{"engineType":"motion_path","engineConfig":{"mode":"fixation","timing":{"holdMs":850,"durationSeconds":60},"content":{"type":"letter","points":3,"pointSize":44,"peripheralCount":2}},"difficultyLevel":1}'::jsonb),
                ('fixation-five', 'Fixation', 'Beş Nokta Sabitleme', 'Beş noktada kısa süreli sabitleme çalış.', '{"engineType":"motion_path","engineConfig":{"mode":"fixation","timing":{"holdMs":1050,"durationSeconds":60},"content":{"type":"number","points":5,"pointSize":42,"peripheralCount":2}},"difficultyLevel":1}'::jsonb),
                ('focus-position', 'Focus', 'Konum Belleği 1-Back', 'Bir önceki konumu hatırla ve karşılaştır.', '{"engineType":"focus","engineConfig":{"mode":"position","nLevel":1,"speedMs":2200,"gridSize":3},"difficultyLevel":1}'::jsonb),
                ('rsvp-calm', 'RSVP', 'Tek Kelime Akışı 180', 'Kelimeleri dakikada 180 kelime temposunda takip et.', '{"engineType":"text_stream","engineConfig":{"mode":"rsvp","pacer":{"speedWpm":180,"showFixationPoint":true},"content":{"source":"reading_text","durationSeconds":180},"visuals":{"fontSize":"large","highlightORP":true}},"difficultyLevel":1}'::jsonb),
                ('rsvp-steady', 'RSVP', 'Tek Kelime Akışı 240', 'Kelimeleri dakikada 240 kelime temposunda takip et.', '{"engineType":"text_stream","engineConfig":{"mode":"rsvp","pacer":{"speedWpm":240,"showFixationPoint":true},"content":{"source":"reading_text","durationSeconds":180},"visuals":{"fontSize":"medium","highlightORP":true}},"difficultyLevel":1}'::jsonb),
                ('saccade-vertical', 'Saccade', 'Dikey Sayı Sıçramaları', 'Dikey hedefler arasında gözlerini sıçrat.', '{"engineType":"motion_path","engineConfig":{"mode":"saccade","timing":{"holdMs":700,"durationSeconds":60},"content":{"type":"number","pattern":"vertical","pointSize":48}},"difficultyLevel":1}'::jsonb),
                ('saccade-horizontal', 'Saccade', 'Yatay Harf Sıçramaları', 'Yatay hedefler arasında gözlerini sıçrat.', '{"engineType":"motion_path","engineConfig":{"mode":"saccade","timing":{"holdMs":650,"durationSeconds":60},"content":{"type":"letter","pattern":"horizontal","pointSize":48}},"difficultyLevel":1}'::jsonb),
                ('scan-words', 'Scanning', 'Anahtar Sözcük Tarama', 'Metindeki hedef sözcükleri süre içinde bul.', '{"engineType":"scanning","engineConfig":{"mode":"word","timeLimit":90,"targetCount":3},"difficultyLevel":1}'::jsonb),
                ('schulte-3', 'SchulteTable', 'Sayı Avcısı 3×3: Sakin Tempo', '1 ile 9 arasındaki sayıları sırayla bul.', '{"engineType":"grid_interaction","engineConfig":{"grid":{"cols":3,"rows":3},"rules":{"timeLimit":90,"interaction":"click_ordered"},"content":{"type":"sequence","range":[1,9],"value":"numbers"},"visuals":{"theme":"colorful","cellSize":"large","highlightCorrect":true}},"difficultyLevel":1}'::jsonb),
                ('schulte-4', 'SchulteTable', 'Sayı Avcısı 4×4: Dikkat', '1 ile 16 arasındaki sayıları sırayla bul.', '{"engineType":"grid_interaction","engineConfig":{"grid":{"cols":4,"rows":4},"rules":{"timeLimit":120,"interaction":"click_ordered"},"content":{"type":"sequence","range":[1,16],"value":"numbers"},"visuals":{"theme":"colorful","cellSize":"medium","highlightCorrect":true}},"difficultyLevel":1}'::jsonb),
                ('flash-letters', 'Tachistoscope', 'Kısa Harf Flaşları', 'Kısa süre gösterilen harfleri algıla.', '{"engineType":"text_stream","engineConfig":{"mode":"flash","timing":{"durationMs":550,"intervalMs":1200},"content":{"type":"letter","count":16},"visuals":{"fontSize":"large","showFixation":true}},"difficultyLevel":1}'::jsonb),
                ('vision-narrow', 'VisualExpansion', 'Dar Açıdan Başlangıç', 'Dar görüş açısıyla başlayıp doğru cevaplarla genişlet.', '{"rounds":20,"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","timing":{"durationMs":950,"intervalMs":1800},"content":{"stimulusType":"letter"}},"startDegrees":8,"targetDegrees":28,"displayDurationMs":950,"difficultyLevel":1}'::jsonb),
                ('vision-medium', 'VisualExpansion', 'Orta Açıdan Genişleme', 'Orta görüş açısında harfleri doğru algıla.', '{"rounds":20,"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","timing":{"durationMs":1100,"intervalMs":2000},"content":{"stimulusType":"number"}},"startDegrees":12,"targetDegrees":36,"displayDurationMs":1100,"difficultyLevel":1}'::jsonb),
                ('visualize-scenes', 'Visualization', 'Sahne Ayrıntılarını Hatırla', 'Kısa süre gösterilen sahnenin ayrıntılarını hatırla.', '{"engineType":"visualization","engineConfig":{"mode":"static","content":{"source":"visualization_scenes","complexity":1}},"difficultyLevel":1}'::jsonb),
                ('free-reading-adult', 'FreeReading', 'Yetişkin Serbest Okuma', 'Yaş grubuna uygun bir metni kendi hızında oku.', '{"engineType":"free_reading","engineConfig":{"mode":"free","content":{"source":"reading_text","minWordCount":200}},"difficultyLevel":1}'::jsonb)
            )
            INSERT INTO speed_reading.exercises
                (id, exercise_type_id, title, description, type_code, difficulty_level,
                 configuration_json, target_age_group_id, creator_id, is_active,
                 created_at, created_by, version, is_deleted)
            SELECT md5('program-catalog-2026-09:' || v.key)::uuid, t.id,
                   v.title, v.description, v.type_name, 1,
                   jsonb_set(v.configuration, '{metadata}',
                       jsonb_build_object('category', lower(v.type_name), 'xpReward', 35, 'estimatedMinutes', 4)),
                   CASE WHEN v.key = 'free-reading-adult'
                        THEN '10000000-0000-0000-0000-000000000003'::uuid ELSE NULL END,
                   'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid,
                   true, now(), 'system:program-catalog-2026-09', 1, false
            FROM variants v
            JOIN speed_reading.exercise_types t ON t.name = v.type_name
                AND t.is_active AND NOT t.is_deleted
            ON CONFLICT (id) DO NOTHING;

            WITH required(age_id, difficulty, amount) AS (
                VALUES
                ('10000000-0000-0000-0000-000000000001'::uuid, 2, 1),
                ('10000000-0000-0000-0000-000000000002'::uuid, 2, 4),
                ('10000000-0000-0000-0000-000000000004'::uuid, 3, 4)
            ),
            chosen AS (
                SELECT r.age_id, r.difficulty, rt.id AS reading_text_id, rt.title,
                       row_number() OVER (PARTITION BY r.age_id ORDER BY rt.id) AS ordinal
                FROM required r
                JOIN speed_reading.reading_texts rt
                  ON rt.target_age_group_id = r.age_id
                 AND rt.difficulty_level <= r.difficulty
                 AND rt.is_active AND NOT rt.is_deleted
                WHERE EXISTS (
                    SELECT 1 FROM speed_reading.reading_questions rq
                    WHERE rq.reading_text_id = rt.id AND NOT rq.is_deleted
                      AND rq.correct_answer IN ('A', 'B', 'C', 'D'))
            )
            INSERT INTO speed_reading.exercises
                (id, exercise_type_id, title, description, type_code, difficulty_level,
                 configuration_json, target_age_group_id, creator_id, is_active,
                 created_at, created_by, version, is_deleted)
            SELECT md5('program-catalog-2026-09:comprehension:' || c.age_id || ':' || c.ordinal)::uuid,
                   t.id, 'Metin Kavrama: ' || c.title,
                   'Metni okuyup anlama sorularını cevapla.', 'Comprehension', c.difficulty,
                   jsonb_build_object(
                       'engineType', 'reading_comprehension',
                       'engineConfig', jsonb_build_object('mode', 'comprehension',
                           'content', jsonb_build_object('source', 'reading_text')),
                       'metadata', jsonb_build_object('category', 'comprehension',
                           'xpReward', 40, 'estimatedMinutes', 5,
                           'targetAgeGroupId', c.age_id,
                           'targetReadingTextId', c.reading_text_id),
                       'difficultyLevel', c.difficulty),
                   c.age_id, 'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid,
                   true, now(), 'system:program-catalog-2026-09', 1, false
            FROM chosen c
            JOIN required r ON r.age_id = c.age_id AND r.difficulty = c.difficulty
            JOIN speed_reading.exercise_types t ON t.name = 'Comprehension'
                AND t.is_active AND NOT t.is_deleted
            WHERE c.ordinal <= r.amount
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Student schedules can reference these exercises; deleting them would corrupt history.
    }
}

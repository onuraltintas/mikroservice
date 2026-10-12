import fs from 'node:fs';
const read = file => JSON.parse(fs.readFileSync(new URL(file, import.meta.url), 'utf8'));
const baseline = read('./fading-baseline.json'), stories = read('./short-stories.json');
const texts = [];
const wordCount = text => text.trim().split(/\s+/u).length;
for (const exercise of baseline) {
  const speed = exercise.configuration.engineConfig.fading.speedWpm;
  const min = Math.ceil(speed * 1.5), max = speed * 2, target = Math.floor((min + max) / 2);
  const previous = exercise.configuration;
  const configuration = structuredClone(previous);
  configuration.engineConfig.content.minWordCount = min;
  configuration.engineConfig.content.maxWordCount = max;
  exercise.previous = previous;
  exercise.configuration = configuration;
  stories.forEach((story, i) => {
    let content = story.paragraph;
    for (const paragraph of story.extensions) {
      if (wordCount(content) >= target) break;
      if (wordCount(content + ' ' + paragraph) > max) break;
      content += '\n\n' + paragraph;
    }
    if (wordCount(content) < min || wordCount(content) > max) throw new Error(`Wrong band: ${exercise.level}/${story.title}/${wordCount(content)}`);
    texts.push({ id: `fa031200-0000-5000-8000-${String(exercise.level * 10 + i + 1).padStart(12, '0')}`,
      exerciseId: exercise.id, ageId: '10000000-0000-0000-0000-000000000001', level: exercise.level,
      title: `${story.title} — kısa çalışma ${exercise.level}`, content, wordCount: wordCount(content), questions: story.questions });
  });
}
fs.writeFileSync(new URL('./short-content.json', import.meta.url), JSON.stringify(texts, null, 2) + '\n');
fs.writeFileSync(new URL('./short-settings.json', import.meta.url), JSON.stringify(baseline, null, 2) + '\n');
const questionRecords = texts.flatMap((text, textIndex) => text.questions.map((q, i) => ({ ...q,
  id: `fa031201-0000-5000-8000-${String(textIndex * 4 + i + 1).padStart(12, '0')}`, textId: text.id, order: i + 1 })));
fs.writeFileSync(new URL('./short-content.sql', import.meta.url), `BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-short-fading-v3'));
CREATE TEMP TABLE short_settings AS SELECT item FROM jsonb_array_elements($settings$${JSON.stringify(baseline)}$settings$::jsonb) item;
CREATE TEMP TABLE short_texts AS SELECT item FROM jsonb_array_elements($texts$${JSON.stringify(texts)}$texts$::jsonb) item;
CREATE TEMP TABLE short_questions AS SELECT item FROM jsonb_array_elements($questions$${JSON.stringify(questionRecords)}$questions$::jsonb) item;
DO $$ BEGIN
IF EXISTS (SELECT 1 FROM short_settings d LEFT JOIN speed_reading.exercises e ON e.id=(item->>'id')::uuid WHERE e.id IS NULL OR e.is_deleted OR NOT e.is_active OR e.is_assessment_only
 OR e.target_age_group_id<>'10000000-0000-0000-0000-000000000001'::uuid OR NOT EXISTS(SELECT 1 FROM speed_reading.exercise_types t WHERE t.id=e.exercise_type_id AND t.name='TextFading' AND t.is_active AND NOT t.is_deleted) OR e.difficulty_level<>(item->>'level')::int OR e.configuration_json NOT IN(item->'previous',item->'configuration')) THEN RAISE EXCEPTION 'Unexpected child fading settings'; END IF;
IF EXISTS (SELECT 1 FROM short_texts d JOIN speed_reading.reading_texts r ON r.id=(item->>'id')::uuid WHERE r.content IS DISTINCT FROM item->>'content' OR r.title IS DISTINCT FROM item->>'title' OR r.exercise_id IS DISTINCT FROM (item->>'exerciseId')::uuid OR r.target_age_group_id IS DISTINCT FROM (item->>'ageId')::uuid OR r.difficulty_level<>(item->>'level')::int OR r.word_count<>(item->>'wordCount')::int OR r.language<>'tr' OR r.is_deleted OR NOT r.is_active) THEN RAISE EXCEPTION 'Short text collision'; END IF;
IF EXISTS (SELECT 1 FROM short_questions d JOIN speed_reading.reading_questions q ON q.id=(item->>'id')::uuid WHERE q.question_text IS DISTINCT FROM item->>'question' OR q.correct_answer IS DISTINCT FROM item->>'correct' OR q.reading_text_id IS DISTINCT FROM (item->>'textId')::uuid OR q.option_a IS DISTINCT FROM item->'options'->>0 OR q.option_b IS DISTINCT FROM item->'options'->>1 OR q.option_c IS DISTINCT FROM item->'options'->>2 OR q.option_d IS DISTINCT FROM item->'options'->>3 OR q.explanation IS DISTINCT FROM item->>'evidence' OR q.type<>1 OR q.bloom_level<>1 OR q.order_index<>(item->>'order')::int OR q.is_deleted) THEN RAISE EXCEPTION 'Short question collision'; END IF;
END $$;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT (item->>'id')::uuid,item->>'title',item->>'content',(item->>'wordCount')::int,'Kısa öykü',(item->>'level')::int,(item->>'ageId')::uuid,'tr',true,'programs-v3-short-fading',(item->>'level')::int,(item->>'level')::int,0,0,0,(item->>'exerciseId')::uuid,now(),'system:programs-v3',1,false FROM short_texts ON CONFLICT(id) DO NOTHING;
INSERT INTO speed_reading.reading_questions(id,reading_text_id,question_text,type,bloom_level,difficulty_level,explanation,option_a,option_b,option_c,option_d,correct_answer,order_index,created_at,created_by,is_deleted)
SELECT (item->>'id')::uuid,(item->>'textId')::uuid,item->>'question',1,1,1,item->>'evidence',item->'options'->>0,item->'options'->>1,item->'options'->>2,item->'options'->>3,item->>'correct',(item->>'order')::int,now(),'system:programs-v3',false FROM short_questions ON CONFLICT(id) DO NOTHING;
UPDATE speed_reading.exercises e SET configuration_json=item->'configuration',updated_at=now(),updated_by='system:programs-v3',version=e.version+1 FROM short_settings d WHERE e.id=(item->>'id')::uuid AND e.configuration_json<>item->'configuration';
COMMIT;
`);
fs.writeFileSync(new URL('./rollback-short-content.sql', import.meta.url), `BEGIN;
CREATE TEMP TABLE short_settings AS SELECT item FROM jsonb_array_elements($settings$${JSON.stringify(baseline)}$settings$::jsonb) item;
DO $$ BEGIN
IF EXISTS (SELECT 1 FROM short_settings d LEFT JOIN speed_reading.exercises e ON e.id=(item->>'id')::uuid WHERE e.id IS NULL OR e.configuration_json NOT IN(item->'previous',item->'configuration')) THEN RAISE EXCEPTION 'Short settings changed after release'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=item->'previous',updated_at=now(),updated_by='system:programs-v3-rollback',version=e.version+1 FROM short_settings d WHERE e.id=(item->>'id')::uuid AND e.configuration_json<>item->'previous';
UPDATE speed_reading.reading_texts SET is_active=false,updated_at=now(),updated_by='system:programs-v3-rollback',version=version+1 WHERE id::text LIKE 'fa031200-0000-5000-8000-%' AND is_active;
COMMIT;
`);

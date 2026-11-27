// Ortak havuza yeni metinler ve 8'er anlama sorusu (26.11.2026).
// Yuvalar targets.json'da (targets.py üretir): her yuva bir yaş, seviye ve kelime aralığıdır. Metinler source/*.json içinde yuva
// numarasıyla yazılır: { "<yuva>": { title, category, content, questions: [{ p, type, bloom, text, correct, wrong[3], explanation }] } }.
// Paragraflar boş satırla ayrılır. Kelime sayısı uygulamanın kuralıyla (boşlukla ayrılan, harf ya da rakam içeren parçalar) hesaplanır.
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { arrange, letterPlan, questionProblems, textProblems } from '../../pool-questions/rules.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
export const ACTOR = 'pool-texts-v1';
export const TARGETS = JSON.parse(readFileSync(join(directory, 'targets.json'), 'utf8'));
export const CATEGORIES = ['Hikaye/Kurgu', 'Bilgi/Açıklama', 'Deneme', 'Hibrit'];
const CHILD = '10000000-0000-0000-0000-000000000001';

export const textId = slot => `d7b60000-0000-5000-8000-${String(slot).padStart(12, '0')}`;
export const questionId = (slot, index) => `d7b70000-0000-5000-8000-${String(slot * 10 + index).padStart(12, '0')}`;
export const paragraphs = content => content.trim().split(/\r?\n\s*\r?\n/u);
// SpeedReading.Domain ReadingText.CalculateWordCount ile aynı.
export const wordCount = content => content.split(/\s+/u).filter(token => /[\p{L}\p{N}]/u.test(token)).length;

const shingles = content => {
  const tokens = content.toLocaleLowerCase('tr').match(/[\p{L}\p{N}]+/gu) ?? [];
  const set = new Set();
  for (let index = 0; index + 6 <= tokens.length; index++) set.add(tokens.slice(index, index + 6).join(' '));
  return set;
};
const shared = (a, b) => { let count = 0; for (const item of a) if (b.has(item)) count++; return count / Math.max(1, Math.min(a.size, b.size)); };

export function loadExisting() {
  const file = resolve(directory, '../../live-snapshot/20261126/texts.jsonl.gz');
  return gunzipSync(readFileSync(file)).toString('utf8').split('\n').filter(Boolean).map(line => JSON.parse(line))
    .filter(row => !row.is_deleted && row.content);
}

export function loadSource() {
  const source = new Map();
  for (const file of readdirSync(join(directory, 'source')).filter(name => name.endsWith('.json')).sort()) {
    for (const [slot, text] of Object.entries(JSON.parse(readFileSync(join(directory, 'source', file), 'utf8')))) {
      const number = Number(slot);
      if (!TARGETS[number - 1]) throw new Error(`yuva ${slot} yok`);
      if (source.has(number)) throw new Error(`yuva ${slot} iki kez yazılmış`);
      source.set(number, text);
    }
  }
  return source;
}

export function build({ partial = false } = {}) {
  const source = loadSource(), texts = [], rows = [], problems = [];
  const existing = loadExisting().map(row => ({ ...row, print: shingles(row.content) }));
  const label = slot => String(slot).padStart(3, '0');
  const fingerprints = [];
  for (const target of TARGETS) {
    const text = source.get(target.slot);
    if (!text) { if (!partial) problems.push(`${label(target.slot)}: metin yok`); continue; }
    const content = text.content.trim(), count = paragraphs(content).length, words = wordCount(content);
    if (!text.title?.trim() || text.title.length > 120) problems.push(`${label(target.slot)}: başlık`);
    if (!CATEGORIES.includes(text.category)) problems.push(`${label(target.slot)}: kategori ${text.category}`);
    if (words < target.min || words > target.max) problems.push(`${label(target.slot)}: ${words} kelime (${target.min}–${target.max})`);
    if (count < 3) problems.push(`${label(target.slot)}: ${count} paragraf (en az 3)`);
    if (/[*_#>`]/u.test(content)) problems.push(`${label(target.slot)}: biçim işareti`);
    const print = shingles(content);
    for (const other of existing) {
      if (other.target_age_group_id && other.target_age_group_id !== target.ageGroupId) continue;
      if (shared(print, other.print) >= 0.3) problems.push(`${label(target.slot)}: canlıdaki "${other.title}" ile örtüşüyor`);
    }
    for (const other of fingerprints) {
      if (shared(print, other.print) >= 0.3) problems.push(`${label(target.slot)}: ${label(other.slot)} ile örtüşüyor`);
      if (other.ageGroupId === target.ageGroupId && other.title === text.title.trim()) problems.push(`${label(target.slot)}: başlık ${label(other.slot)} ile aynı`);
    }
    fingerprints.push({ slot: target.slot, print, ageGroupId: target.ageGroupId, title: text.title.trim() });
    const young = target.ageGroupId === CHILD && target.level <= 2;
    for (const problem of textProblems(text.questions, count, { young })) problems.push(`${label(target.slot)}: ${problem}`);
    const id = textId(target.slot);
    texts.push({ id, slot: target.slot, title: text.title.trim(), category: text.category, content, wordCount: words,
      contentMd5: createHash('md5').update(content, 'utf8').digest('hex'),
      level: target.level, ageGroupId: target.ageGroupId, min: target.min, max: target.max });
    const plan = letterPlan(id);
    text.questions.forEach((question, index) => {
      for (const problem of questionProblems(question, count)) problems.push(`${label(target.slot)} #${index + 1}: ${problem}`);
      const { options, answer } = arrange(question, plan[index]);
      rows.push({ id: questionId(target.slot, index), readingTextId: id, paragraph: question.p, questionText: question.text,
        type: question.type, bloomLevel: question.bloom, difficultyLevel: target.level, explanation: question.explanation,
        optionA: options[0], optionB: options[1], optionC: options[2], optionD: options[3], correctAnswer: answer, orderIndex: index });
    });
  }
  return { texts, rows, problems };
}

const sql = value => value === null ? 'NULL' : typeof value === 'number' ? String(value) : `'${String(value).replaceAll("'", "''")}'`;
export function render(texts, rows) {
  const apply = `-- Generated by content-packs/pool-texts/v1/generate.mjs. Do not edit by hand.
-- Ortak havuza ${texts.length} yeni metin ve ${rows.length} anlama sorusu. Yalnız speedreading_owned_db.
-- Bu kimliklerden biri başka bir kaynaktan ya da başka içerikle zaten varsa hiçbir şey değiştirmeden durur.
-- Tekrar çalıştırmak güvenli: var olan satır atlanır; geri almayla silinenler geri gelir.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';

CREATE TEMP TABLE pool_text (
    id uuid PRIMARY KEY, title text NOT NULL, content text NOT NULL, content_md5 text NOT NULL, word_count integer NOT NULL,
    category text NOT NULL, difficulty_level integer NOT NULL, target_age_group_id uuid NOT NULL
) ON COMMIT DROP;
INSERT INTO pool_text (id, title, content, content_md5, word_count, category, difficulty_level, target_age_group_id) VALUES
${texts.map(text => `    ('${text.id}'::uuid, ${sql(text.title)}, ${sql(text.content)}, '${text.contentMd5}', ${text.wordCount}, ${sql(text.category)}, ${text.level}, '${text.ageGroupId}'::uuid)`).join(',\n')};

CREATE TEMP TABLE pool_question (
    id uuid PRIMARY KEY, reading_text_id uuid NOT NULL, question_text text NOT NULL, type integer NOT NULL,
    bloom_level integer NOT NULL, difficulty_level integer NOT NULL, explanation text NOT NULL,
    option_a text NOT NULL, option_b text NOT NULL, option_c text NOT NULL, option_d text NOT NULL,
    correct_answer text NOT NULL, order_index integer NOT NULL
) ON COMMIT DROP;
INSERT INTO pool_question
    (id, reading_text_id, question_text, type, bloom_level, difficulty_level, explanation,
     option_a, option_b, option_c, option_d, correct_answer, order_index)
VALUES
${rows.map(row => `    ('${row.id}'::uuid, '${row.readingTextId}'::uuid, ${sql(row.questionText)}, ${row.type}, ${row.bloomLevel}, ${row.difficultyLevel}, ${sql(row.explanation)},
     ${sql(row.optionA)}, ${sql(row.optionB)}, ${sql(row.optionC)}, ${sql(row.optionD)}, '${row.correctAnswer}', ${row.orderIndex})`).join(',\n')};

DO $$
DECLARE conflicts integer; added_texts integer; restored_texts integer; added integer; restored integer;
BEGIN
    SELECT count(*) INTO conflicts FROM pool_text s JOIN speed_reading.reading_texts t ON t.id = s.id
    WHERE t.created_by IS DISTINCT FROM '${ACTOR}' OR md5(t.content) <> s.content_md5;
    SELECT conflicts + count(*) INTO conflicts FROM pool_question s JOIN speed_reading.reading_questions q ON q.id = s.id
    WHERE q.created_by IS DISTINCT FROM '${ACTOR}';
    SELECT conflicts + count(*) INTO conflicts FROM speed_reading.reading_questions q JOIN pool_text s ON s.id = q.reading_text_id
    WHERE NOT q.is_deleted AND q.created_by IS DISTINCT FROM '${ACTOR}';
    IF conflicts <> 0 THEN
        RAISE EXCEPTION '${ACTOR}: % conflicting row(s) already exist; nothing changed', conflicts;
    END IF;

    INSERT INTO speed_reading.reading_texts
        (id, title, content, word_count, category, difficulty_level, target_age_group_id, language, is_active, tags,
         recommended_min_level, recommended_max_level, average_comprehension_score, times_read, average_reading_time_seconds,
         exercise_id, created_at, created_by, version, is_deleted)
    SELECT id, title, content, word_count, category, difficulty_level, target_age_group_id, 'tr', TRUE, '${ACTOR}',
           difficulty_level, difficulty_level, 0, 0, 0, NULL, CURRENT_TIMESTAMP, '${ACTOR}', 1, FALSE
    FROM pool_text s WHERE NOT EXISTS (SELECT 1 FROM speed_reading.reading_texts t WHERE t.id = s.id);
    GET DIAGNOSTICS added_texts = ROW_COUNT;
    UPDATE speed_reading.reading_texts t
    SET is_deleted = FALSE, deleted_at = NULL, deleted_by = NULL, updated_at = CURRENT_TIMESTAMP, updated_by = '${ACTOR}'
    FROM pool_text s WHERE t.id = s.id AND t.is_deleted AND t.deleted_by = '${ACTOR}-rollback';
    GET DIAGNOSTICS restored_texts = ROW_COUNT;

    INSERT INTO speed_reading.reading_questions
        (id, reading_text_id, question_text, type, bloom_level, difficulty_level, explanation,
         option_a, option_b, option_c, option_d, correct_answer, order_index, created_at, created_by, is_deleted)
    SELECT id, reading_text_id, question_text, type, bloom_level, difficulty_level, explanation,
           option_a, option_b, option_c, option_d, correct_answer, order_index, CURRENT_TIMESTAMP, '${ACTOR}', FALSE
    FROM pool_question s WHERE NOT EXISTS (SELECT 1 FROM speed_reading.reading_questions q WHERE q.id = s.id);
    GET DIAGNOSTICS added = ROW_COUNT;
    UPDATE speed_reading.reading_questions q
    SET is_deleted = FALSE, deleted_at = NULL, deleted_by = NULL, updated_at = CURRENT_TIMESTAMP, updated_by = '${ACTOR}'
    FROM pool_question s WHERE q.id = s.id AND q.is_deleted AND q.deleted_by = '${ACTOR}-rollback';
    GET DIAGNOSTICS restored = ROW_COUNT;
    RAISE NOTICE '${ACTOR}: % text(s) added, % restored; % question(s) added, % restored', added_texts, restored_texts, added, restored;
END $$;

COMMIT;
`;
  const rollback = `-- Generated by content-packs/pool-texts/v1/generate.mjs. Do not edit by hand.
-- Yalnız bu paketin ${texts.length} metnini ve ${rows.length} sorusunu yumuşak siler.
BEGIN;
DO $$
DECLARE removed_texts integer; removed integer;
BEGIN
    UPDATE speed_reading.reading_questions
    SET is_deleted = TRUE, deleted_at = CURRENT_TIMESTAMP, deleted_by = '${ACTOR}-rollback',
        updated_at = CURRENT_TIMESTAMP, updated_by = '${ACTOR}-rollback'
    WHERE NOT is_deleted AND created_by = '${ACTOR}' AND reading_text_id IN (
${texts.map(text => `        '${text.id}'::uuid`).join(',\n')});
    GET DIAGNOSTICS removed = ROW_COUNT;
    UPDATE speed_reading.reading_texts
    SET is_deleted = TRUE, deleted_at = CURRENT_TIMESTAMP, deleted_by = '${ACTOR}-rollback',
        updated_at = CURRENT_TIMESTAMP, updated_by = '${ACTOR}-rollback'
    WHERE NOT is_deleted AND created_by = '${ACTOR}' AND id IN (
${texts.map(text => `        '${text.id}'::uuid`).join(',\n')});
    GET DIAGNOSTICS removed_texts = ROW_COUNT;
    RAISE NOTICE '${ACTOR} rollback: % text(s), % question(s) soft-deleted', removed_texts, removed;
END $$;
COMMIT;
`;
  return { apply, rollback };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const partial = process.argv.includes('--check');
  const { texts, rows, problems } = build({ partial });
  if (problems.length) { console.error(problems.join('\n')); process.exitCode = 1; }
  if (partial) console.log(`${texts.length} text(s) checked, ${problems.length} problem(s)`);
  else if (!problems.length) {
    const { apply, rollback } = render(texts, rows);
    writeFileSync(join(directory, 'texts.json'), JSON.stringify(texts, null, 2) + '\n');
    writeFileSync(join(directory, 'questions.json'), JSON.stringify(rows, null, 2) + '\n');
    writeFileSync(join(directory, 'apply.sql'), apply);
    writeFileSync(join(directory, 'rollback.sql'), rollback);
    console.log(`Generated ${texts.length} texts and ${rows.length} questions.`);
  }
}

// Ortak metin havuzu v1 (26.11.2026). Kaynak: content-packs/live-snapshot/20261126 (canlı dışa aktarım).
// Düz okuma egzersizlerine bağlı metinlerin bağı kaldırılır; aynı öykünün başka bir seçkisi o yaşta zaten ortak havuzdaysa
// ya da öykü bir Okuduğunu Anlama değerlendirme metniyle aynıysa metin taşınmaz. Kelime Gruplama'nın kelime aralıkları
// ortak havuzdaki bütün metinleri kapsayacak kadar genişletilir.
import { readFileSync, writeFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = dirname(fileURLToPath(import.meta.url));
const snapshot = resolve(directory, '../../live-snapshot/20261126');
const load = name => gunzipSync(readFileSync(join(snapshot, `${name}.jsonl.gz`))).toString('utf8')
  .split('\n').filter(Boolean).map(line => JSON.parse(line));

export const SHAREABLE = ['FreeReading', 'Chunking', 'RegressionReduction', 'SubvocalizationReduction', 'RSVP', 'TextFading',
  'Comprehension:learning'];
export const AGES = { '10000000-0000-0000-0000-000000000001': 'Çocuk', '10000000-0000-0000-0000-000000000002': 'Genç',
  '10000000-0000-0000-0000-000000000004': 'Genç yetişkin', '10000000-0000-0000-0000-000000000003': 'Yetişkin' };
export const OVERLAP = 0.3, SHINGLE = 6;

const config = exercise => {
  const raw = exercise.configuration_json ?? {};
  return typeof raw === 'string' ? JSON.parse(raw) : raw;
};
export function validQuestion(question) {
  const answer = (question.correct_answer ?? '').trim().toUpperCase();
  return !question.is_deleted && [1, 2, 3].includes(question.type) && question.bloom_level >= 1 && question.bloom_level <= 6
    && ['A', 'B', 'C', 'D'].includes(answer)
    && ['option_a', 'option_b', 'option_c', 'option_d'].every(key => (question[key] ?? '').trim() !== '');
}
export function shingles(content) {
  const words = content.toLocaleLowerCase('tr').match(/[\p{L}\p{N}]+/gu) ?? [];
  const set = new Set();
  for (let index = 0; index + SHINGLE <= words.length; index++) set.add(words.slice(index, index + SHINGLE).join(' '));
  return set;
}

export function build() {
  const texts = load('texts').filter(text => text.is_active && !text.is_deleted && (text.content ?? '').trim());
  const exercises = new Map(load('exercises').map(exercise => [exercise.id, exercise]));
  const questions = new Map();
  for (const question of load('questions')) if (validQuestion(question))
    questions.set(question.reading_text_id, (questions.get(question.reading_text_id) ?? 0) + 1);
  const binding = text => {
    if (!text.exercise_id) return 'shared';
    const exercise = exercises.get(text.exercise_id);
    if (!exercise) throw new Error(`text ${text.id} is bound to a missing exercise`);
    if (exercise.type_name !== 'Comprehension') return exercise.type_name;
    const engine = config(exercise).engineConfig ?? {};
    return `Comprehension:${engine.comprehensionMode ?? engine.readingPurpose}`;
  };

  // Aynı öykü kümeleri: 6 kelimelik dizilerin en az %30'u ortak olan metinler (iki yönden birinde).
  const sets = new Map(texts.map(text => [text.id, shingles(text.content)]));
  const owners = new Map();
  for (const [id, set] of sets) for (const gram of set) { const list = owners.get(gram) ?? []; list.push(id); owners.set(gram, list); }
  const shared = new Map();
  for (const list of owners.values()) {
    if (list.length < 2 || list.length > 40) continue;
    for (let i = 0; i < list.length; i++) for (let j = i + 1; j < list.length; j++) {
      const key = list[i] < list[j] ? `${list[i]}|${list[j]}` : `${list[j]}|${list[i]}`;
      shared.set(key, (shared.get(key) ?? 0) + 1);
    }
  }
  const parent = new Map(texts.map(text => [text.id, text.id]));
  const find = id => { while (parent.get(id) !== id) { parent.set(id, parent.get(parent.get(id))); id = parent.get(id); } return id; };
  for (const [key, count] of shared) {
    const [a, b] = key.split('|');
    if (Math.max(count / Math.max(1, sets.get(a).size), count / Math.max(1, sets.get(b).size)) >= OVERLAP) parent.set(find(a), find(b));
  }
  const cluster = new Map(texts.map(text => [text.id, find(text.id)]));
  const clusterBindings = new Map();
  for (const text of texts) {
    const set = clusterBindings.get(cluster.get(text.id)) ?? new Set();
    set.add(binding(text)); clusterBindings.set(cluster.get(text.id), set);
  }

  const taken = new Map();
  const take = (text) => { const set = taken.get(cluster.get(text.id)) ?? new Set(); set.add(text.target_age_group_id ?? '*'); taken.set(cluster.get(text.id), set); };
  for (const text of texts) if (binding(text) === 'shared') take(text);
  const candidates = texts.filter(text => SHAREABLE.includes(binding(text)))
    .sort((a, b) => (questions.get(b.id) ?? 0) - (questions.get(a.id) ?? 0) || (a.id < b.id ? -1 : 1));
  const moves = [], skipped = [];
  for (const text of candidates) {
    const ages = taken.get(cluster.get(text.id)) ?? new Set();
    const reason = clusterBindings.get(cluster.get(text.id)).has('Comprehension:evaluation') ? 'same story as an evaluation text'
      : ages.has('*') || ages.has(text.target_age_group_id ?? '*') || (text.target_age_group_id === null && ages.size) ? 'same story already in the pool for this age'
      : null;
    if (reason) { skipped.push({ id: text.id, source: binding(text), reason }); continue; }
    take(text);
    moves.push({ id: text.id, exerciseId: text.exercise_id, source: binding(text), contentMd5: text.content_md5,
      ageGroupId: text.target_age_group_id, level: text.difficulty_level, wordCount: text.word_count,
      questions: questions.get(text.id) ?? 0 });
  }

  // Kelime Gruplama aralıkları: mevcut aralık ile havuzdaki bütün metinlerin en kısası–en uzunu (10'a yuvarlı).
  const moved = new Set(moves.map(move => move.id));
  const pool = texts.filter(text => binding(text) === 'shared' || moved.has(text.id));
  const bands = [...exercises.values()]
    .filter(exercise => exercise.type_name === 'Chunking' && !exercise.is_deleted && !exercise.is_assessment_only)
    .map(exercise => {
      const content = config(exercise).engineConfig.content;
      const counts = pool.filter(text => text.difficulty_level === exercise.difficulty_level
        && (text.target_age_group_id === null || text.target_age_group_id === exercise.target_age_group_id)).map(text => text.word_count);
      return { id: exercise.id, age: AGES[exercise.target_age_group_id], level: exercise.difficulty_level,
        from: [content.minWordCount, content.maxWordCount],
        to: [Math.min(content.minWordCount, Math.floor(Math.min(...counts) / 10) * 10),
          Math.max(content.maxWordCount, Math.ceil(Math.max(...counts) / 10) * 10)] };
    }).sort((a, b) => Object.values(AGES).indexOf(a.age) - Object.values(AGES).indexOf(b.age) || a.level - b.level);
  return { moves, skipped, bands, pool: pool.length };
}

const sql = value => `'${String(value).replaceAll("'", "''")}'`;
export function render({ moves, bands }) {
  const apply = `-- Generated by content-packs/shared-pool/v1/generate.mjs. Do not edit by hand.
-- Ortak metin havuzu v1: ${moves.length} düz okuma metninin egzersiz bağı kaldırılır; ${bands.length} Kelime Gruplama
-- egzersizinin kelime aralığı genişler. Yalnız speedreading_owned_db. Tek işlem; beklenen durum yoksa hiçbir şey değişmez.
-- Tekrar çalıştırmak güvenli (zaten taşınmış metin ve zaten genişlemiş aralık atlanır).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';

CREATE TEMP TABLE shared_pool_move (id uuid PRIMARY KEY, exercise_id uuid NOT NULL, content_md5 text NOT NULL) ON COMMIT DROP;
INSERT INTO shared_pool_move (id, exercise_id, content_md5) VALUES
${moves.map(move => `    ('${move.id}'::uuid, '${move.exerciseId}'::uuid, '${move.contentMd5}')`).join(',\n')};

CREATE TEMP TABLE shared_pool_band (id uuid PRIMARY KEY, from_min integer NOT NULL, from_max integer NOT NULL,
    to_min integer NOT NULL, to_max integer NOT NULL) ON COMMIT DROP;
INSERT INTO shared_pool_band (id, from_min, from_max, to_min, to_max) VALUES
${bands.map(band => `    ('${band.id}'::uuid, ${band.from[0]}, ${band.from[1]}, ${band.to[0]}, ${band.to[1]})`).join(',\n')};

DO $$
DECLARE ready integer; banded integer;
BEGIN
    SELECT count(*) INTO ready FROM shared_pool_move m
    JOIN speed_reading.reading_texts t ON t.id = m.id AND NOT t.is_deleted AND t.is_active AND md5(t.content) = m.content_md5
        AND (t.exercise_id = m.exercise_id OR t.exercise_id IS NULL);
    IF ready <> ${moves.length} THEN
        RAISE EXCEPTION 'shared-pool/v1: % of ${moves.length} texts are in the expected state; nothing changed', ready;
    END IF;
    SELECT count(*) INTO banded FROM shared_pool_band b
    JOIN speed_reading.exercises e ON e.id = b.id AND NOT e.is_deleted
    WHERE ((e.configuration_json #>> '{engineConfig,content,minWordCount}')::integer, (e.configuration_json #>> '{engineConfig,content,maxWordCount}')::integer)
        IN ((b.from_min, b.from_max), (b.to_min, b.to_max));
    IF banded <> ${bands.length} THEN
        RAISE EXCEPTION 'shared-pool/v1: % of ${bands.length} Kelime Gruplama exercises hold the expected word range; nothing changed', banded;
    END IF;
END $$;

DO $$
DECLARE moved integer; widened integer;
BEGIN
    UPDATE speed_reading.reading_texts t SET exercise_id = NULL, updated_at = CURRENT_TIMESTAMP, updated_by = 'shared-pool-v1',
        version = t.version + 1
    FROM shared_pool_move m WHERE t.id = m.id AND t.exercise_id = m.exercise_id;
    GET DIAGNOSTICS moved = ROW_COUNT;
    UPDATE speed_reading.exercises e SET configuration_json = jsonb_set(jsonb_set(e.configuration_json,
            '{engineConfig,content,minWordCount}', to_jsonb(b.to_min)), '{engineConfig,content,maxWordCount}', to_jsonb(b.to_max)),
        updated_at = CURRENT_TIMESTAMP, updated_by = 'shared-pool-v1', version = e.version + 1
    FROM shared_pool_band b WHERE e.id = b.id
        AND (e.configuration_json #>> '{engineConfig,content,minWordCount}')::integer = b.from_min
        AND (e.configuration_json #>> '{engineConfig,content,maxWordCount}')::integer = b.from_max
        AND (b.from_min, b.from_max) IS DISTINCT FROM (b.to_min, b.to_max);
    GET DIAGNOSTICS widened = ROW_COUNT;
    RAISE NOTICE 'shared-pool/v1: % text(s) moved to the shared pool, % Kelime Gruplama range(s) widened', moved, widened;
END $$;

COMMIT;
`;
  const rollback = `-- Generated by content-packs/shared-pool/v1/generate.mjs. Do not edit by hand.
-- Ortak metin havuzu v1'i geri alır: metinler eski egzersizlerine bağlanır, Kelime Gruplama aralıkları eski değerine döner.
-- Yalnız bu paketin değiştirdiği ve o günden beri başka bir şeyin değiştirmediği satırlara dokunur.
BEGIN;
SET LOCAL lock_timeout = '5s';
CREATE TEMP TABLE shared_pool_move (id uuid PRIMARY KEY, exercise_id uuid NOT NULL) ON COMMIT DROP;
INSERT INTO shared_pool_move (id, exercise_id) VALUES
${moves.map(move => `    ('${move.id}'::uuid, '${move.exerciseId}'::uuid)`).join(',\n')};
CREATE TEMP TABLE shared_pool_band (id uuid PRIMARY KEY, from_min integer NOT NULL, from_max integer NOT NULL,
    to_min integer NOT NULL, to_max integer NOT NULL) ON COMMIT DROP;
INSERT INTO shared_pool_band (id, from_min, from_max, to_min, to_max) VALUES
${bands.map(band => `    ('${band.id}'::uuid, ${band.from[0]}, ${band.from[1]}, ${band.to[0]}, ${band.to[1]})`).join(',\n')};
DO $$
DECLARE restored integer; narrowed integer;
BEGIN
    UPDATE speed_reading.reading_texts t SET exercise_id = m.exercise_id, updated_at = CURRENT_TIMESTAMP,
        updated_by = 'shared-pool-v1-rollback', version = t.version + 1
    FROM shared_pool_move m WHERE t.id = m.id AND t.exercise_id IS NULL AND t.updated_by = 'shared-pool-v1';
    GET DIAGNOSTICS restored = ROW_COUNT;
    UPDATE speed_reading.exercises e SET configuration_json = jsonb_set(jsonb_set(e.configuration_json,
            '{engineConfig,content,minWordCount}', to_jsonb(b.from_min)), '{engineConfig,content,maxWordCount}', to_jsonb(b.from_max)),
        updated_at = CURRENT_TIMESTAMP, updated_by = 'shared-pool-v1-rollback', version = e.version + 1
    FROM shared_pool_band b WHERE e.id = b.id AND e.updated_by = 'shared-pool-v1'
        AND (e.configuration_json #>> '{engineConfig,content,minWordCount}')::integer = b.to_min
        AND (e.configuration_json #>> '{engineConfig,content,maxWordCount}')::integer = b.to_max;
    GET DIAGNOSTICS narrowed = ROW_COUNT;
    RAISE NOTICE 'shared-pool/v1 rollback: % text(s) re-bound, % range(s) restored', restored, narrowed;
END $$;
COMMIT;
`;
  return { apply, rollback };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = build();
  const { apply, rollback } = render(result);
  writeFileSync(join(directory, 'moves.json'), JSON.stringify({ moves: result.moves, skipped: result.skipped }, null, 2) + '\n');
  writeFileSync(join(directory, 'bands.json'), JSON.stringify(result.bands, null, 2) + '\n');
  writeFileSync(join(directory, 'apply.sql'), apply);
  writeFileSync(join(directory, 'rollback.sql'), rollback);
  console.log(`${result.moves.length} moves, ${result.skipped.length} skipped, ${result.bands.length} bands, pool ${result.pool}`);
}

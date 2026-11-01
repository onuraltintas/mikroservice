import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/coaching-catalog-km3 (KM3, 01.11.2026) on a disposable copy of the production coaching catalog:
//   COACHING_CATALOG_KM3_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres \
//   COACHING_CATALOG_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 \
//   node --test tests/E2E/coaching-catalog-km3.postgres.test.mjs
// The snapshot directory is the read-only catalog export (schema.sql + one CSV per table); it is never committed.
const serverUrl = process.env.COACHING_CATALOG_KM3_TEST_DATABASE_URL;
const snapshot = process.env.COACHING_CATALOG_SNAPSHOT_DIR;
const skip = !(serverUrl && snapshot) && 'set COACHING_CATALOG_KM3_TEST_DATABASE_URL and COACHING_CATALOG_SNAPSHOT_DIR';
const pack = name => readFileSync(new URL(`../../content-packs/coaching-catalog-km3/${name}`, import.meta.url), 'utf8');
const database = `coaching_catalog_km3_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (input, url = databaseUrl()) => spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'],
  { input, encoding: 'utf8', timeout: 300000, maxBuffer: 64 * 1024 * 1024 });
const run = (input, url) => {
  const result = psql(input, url);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result.stdout.trim();
};
const notices = result => result.stderr.split('\n').filter(x => x.startsWith('NOTICE:')).map(x => x.slice(8).trim());

// Every catalog row that is not ours, so the test can prove the pack leaves the existing catalog alone.
const foreignRows = () => run(`
  SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
    SELECT l::text x FROM coaching.study_catalog_lessons l WHERE "Source" <> 'coaching-catalog-km3'
    UNION ALL SELECT u::text FROM coaching.study_catalog_units u WHERE "Source" <> 'coaching-catalog-km3'
    UNION ALL SELECT t::text FROM coaching.study_catalog_topics t WHERE "Source" <> 'coaching-catalog-km3'
    UNION ALL SELECT o::text FROM coaching.study_catalog_outcomes o JOIN coaching.official_curriculum_editions e ON e."Id" = o."EditionId" WHERE e."Code" NOT LIKE 'km3-%'
    UNION ALL SELECT e::text FROM coaching.official_curriculum_editions e WHERE e."Code" NOT LIKE 'km3-%'
    UNION ALL SELECT c::text FROM coaching.curriculum_versions c WHERE c."Code" <> 'coaching-exam-prep-km3-2026'
    UNION ALL SELECT x::text FROM coaching.study_catalog_exams x WHERE "Code" NOT LIKE 'OABT\\_%') rows;`);

const counts = () => Object.fromEntries(run(`
  SELECT CASE WHEN l."ExamCode" LIKE 'OABT\\_%' THEN 'OABT' ELSE l."ExamCode" END, count(DISTINCT l."Id") || '/' || count(DISTINCT u."Id") || '/' || count(DISTINCT t."Id") || '/' || count(DISTINCT m."OutcomeId")
  FROM coaching.study_catalog_lessons l
  JOIN coaching.study_catalog_units u ON u."LessonId" = l."Id"
  JOIN coaching.study_catalog_topics t ON t."UnitId" = u."Id"
  LEFT JOIN coaching.study_catalog_topic_outcomes m ON m."TopicId" = t."Id"
  WHERE l."Source" = 'coaching-catalog-km3' GROUP BY 1 ORDER BY 1;`).split('\n').filter(Boolean).map(x => x.split('|')));

let work;
before(() => {
  if (skip) return;
  work = mkdtempSync(join(tmpdir(), 'km3-'));
  run(`DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`, serverUrl);
  // The export references the owner role, a deletion trigger function and the duration-estimate table it does not include.
  const schema = readFileSync(join(snapshot, 'schema.sql'), 'utf8').split('\n')
    .filter(x => !/^\\(un)?restrict /.test(x) && !/ OWNER TO /.test(x)).join('\n');
  writeFileSync(join(work, 'schema.sql'), schema);
  run(`CREATE SCHEMA coaching;
    CREATE TABLE coaching.outcome_duration_estimates ("Id" uuid NOT NULL, "OutcomeId" uuid NOT NULL, UNIQUE ("Id", "OutcomeId"));
    CREATE FUNCTION coaching.guard_catalog_exam_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN OLD; END $$;
    \\i ${join(work, 'schema.sql')}`);
  const tables = readdirSync(snapshot).filter(x => x.endsWith('.csv'))
    .map(x => x.slice(0, -4)).filter(x => x !== 'study_catalog_topic_outcome_shares'); // needs the estimates the export leaves out
  const order = ['curriculum_versions', 'study_catalog_exams', 'study_catalog_lessons', 'study_catalog_units', 'study_catalog_topics',
    'official_curriculum_editions', 'official_curriculum_topics', 'official_curriculum_lesson_bindings', 'study_catalog_outcomes',
    'study_catalog_outcome_sources', 'study_catalog_topic_outcomes', 'study_catalog_yks_scopes', 'score_models', 'target_catalog'];
  for (const table of order.filter(x => tables.includes(x))) {
    const file = join(snapshot, `${table}.csv`);
    const header = readFileSync(file, 'utf8').split('\n', 1)[0].split(',').map(x => `"${x}"`).join(',');
    run(`\\copy coaching.${table} (${header}) FROM '${file}' WITH (FORMAT csv, HEADER true)`);
  }
});
after(() => {
  if (skip) return;
  run(`DROP DATABASE IF EXISTS ${database};`, serverUrl);
  rmSync(work, { recursive: true, force: true });
});

test('stops without changing anything while the KM1 exams AGS, OABT, YDS and YOKDIL are missing', { skip }, () => {
  const before = foreignRows();
  const result = psql(pack('apply.sql'));
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /KM1/);
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons WHERE "Source" = 'coaching-catalog-km3'`), '0');
  assert.equal(foreignRows(), before);
});

test('loads the AGS, ÖABT, YDS and YÖKDİL drafts once, passive and pending approval, and leaves the catalog alone', { skip }, () => {
  // What the KM1 exam-calendar pack adds (content-packs/exam-calendar/2027, feat/km1-exam-types): the four exams, passive.
  run(`INSERT INTO coaching.study_catalog_exams ("Id","Code","Name","DisplayOrder","IsActive") VALUES
    ('f0ca1000-0000-4000-8000-000000000006','AGS','Akademi Giriş Sınavı',5,false),
    ('f0ca1000-0000-4000-8000-000000000007','OABT','Öğretmenlik Alan Bilgisi Testi',6,false),
    ('f0ca1000-0000-4000-8000-000000000008','YDS','Yabancı Dil Bilgisi Seviye Tespit Sınavı',7,false),
    ('f0ca1000-0000-4000-8000-000000000009','YOKDIL','Yükseköğretim Kurumları Yabancı Dil Sınavı',8,false);`);
  const foreign = foreignRows();
  const first = psql(pack('apply.sql'));
  assert.equal(first.status, 0, first.stderr);
  const firstNotices = notices(first);
  assert.ok(firstNotices.some(x => /^before: lessons 169, units 768, topics 5220, outcomes 3453/.test(x)), firstNotices.join('\n'));
  assert.ok(firstNotices.some(x => /^after: lessons 196, units 825, topics 5477, /.test(x)), firstNotices.join('\n'));
  // lessons / units / leaf+group topics / outcomes per exam
  assert.deepEqual(counts(), { AGS: '6/6/29/29', OABT: '17/20/146/146', YDS: '1/10/22/22', YOKDIL: '3/21/60/60' });
  // KM1 decision: an ÖABT lesson's ExamCode is its field code; the 17 codes are added as passive exams (FK).
  assert.equal(run(`SELECT string_agg(e."Code" || ':' || e."IsActive" || ':' || l."SourceId", ',' ORDER BY e."Code") FROM coaching.study_catalog_exams e
    JOIN coaching.study_catalog_lessons l ON l."ExamCode" = e."Code" AND l."Source" = 'coaching-catalog-km3' WHERE e."Code" LIKE 'OABT\\_%'`),
    ['OABT_BED', 'OABT_BIO', 'OABT_COG', 'OABT_DIN', 'OABT_FEN', 'OABT_FIZ', 'OABT_IMAT', 'OABT_KIM', 'OABT_MAT', 'OABT_OKUL', 'OABT_OZEL',
      'OABT_REH', 'OABT_SINIF', 'OABT_SOS', 'OABT_TAR', 'OABT_TDE', 'OABT_TUR'].map(x => `${x}:false:${x}`).join(','));
  assert.equal(run(`SELECT "Name" FROM coaching.study_catalog_exams WHERE "Code" = 'OABT_OZEL'`), 'ÖABT · Özel Eğitim');
  assert.equal(foreignRows(), foreign, 'existing lessons, units, topics, outcomes, editions, versions and exams are unchanged');

  // Passive: nothing a student can see until the team approves it in the admin panel.
  assert.equal(run(`SELECT count(*) FILTER (WHERE "IsActive") || '/' || count(*) FROM coaching.study_catalog_lessons WHERE "Source" = 'coaching-catalog-km3'`), '0/27');
  assert.equal(run(`SELECT count(*) FILTER (WHERE "IsActive") FROM coaching.study_catalog_units WHERE "Source" = 'coaching-catalog-km3'`), '0');
  assert.equal(run(`SELECT count(*) FILTER (WHERE "IsActive") FROM coaching.study_catalog_topics WHERE "Source" = 'coaching-catalog-km3'`), '0');
  assert.equal(run(`SELECT count(*) FILTER (WHERE m."IsApproved") FROM coaching.study_catalog_topic_outcomes m
    JOIN coaching.study_catalog_topics t ON t."Id" = m."TopicId" WHERE t."Source" = 'coaching-catalog-km3'`), '0');
  assert.equal(run(`SELECT "IsPublished" || ':' || "ValidFromYear" || '-' || "ValidToYear" FROM coaching.curriculum_versions WHERE "Code" = 'coaching-exam-prep-km3-2026'`), 'false:2026-2027');
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons WHERE "Source" = 'coaching-catalog-km3'
    AND "CurriculumVersionId" = (SELECT "Id" FROM coaching.curriculum_versions WHERE "Code" = 'coaching-exam-prep-km3-2026')`), '27');
  // Sources on every outcome; weights and planner limits on every topic.
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_outcomes o JOIN coaching.official_curriculum_editions e ON e."Id" = o."EditionId"
    WHERE e."Code" LIKE 'km3-%' AND (o."SourceKind" <> 'ExamSkill' OR o."SourceUrl" NOT LIKE 'https://dokuman.osym.gov.tr/%')`), '0');
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_topics t WHERE "Source" = 'coaching-catalog-km3'
    AND NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId" = t."Id")
    AND ("EstimatedMinutes" IS NULL OR "EstimatedMinutes" NOT BETWEEN 30 AND 1440)`), '0');
  assert.equal(run(`SELECT "YieldWeight" || ':' || "EstimatedMinutes" FROM coaching.study_catalog_topics WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'OABT_OZEL.1.01'`), '5:900');

  // verify.sql (read-only) agrees.
  const verify = run(pack('verify.sql'));
  assert.match(verify, /AGS\|6\|6\|29\|29\|0 active/);
  assert.match(verify, /km3 verify: ok/);

  // Running it again changes nothing.
  const again = psql(pack('apply.sql'));
  assert.equal(again.status, 0, again.stderr);
  assert.ok(notices(again).some(x => /^inserted: lessons 0, units 0, topics 0, outcomes 0, editions 0, mappings 0, ÖABT field exams 0/.test(x)), notices(again).join('\n'));
  assert.deepEqual(counts(), { AGS: '6/6/29/29', OABT: '17/20/146/146', YDS: '1/10/22/22', YOKDIL: '3/21/60/60' });
});

test('after the team activates and approves records, a rerun keeps their decisions; changed content stops it', { skip }, () => {
  run(`UPDATE coaching.study_catalog_lessons SET "IsActive" = true WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'AGS_SOZ';
    UPDATE coaching.study_catalog_topic_outcomes m SET "IsApproved" = true, "ReviewedAt" = now()
    FROM coaching.study_catalog_topics t WHERE t."Id" = m."TopicId" AND t."SourceId" = 'AGS_SOZ.1.01';`);
  const rerun = psql(pack('apply.sql'));
  assert.equal(rerun.status, 0, rerun.stderr);
  assert.equal(run(`SELECT "IsActive" FROM coaching.study_catalog_lessons WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'AGS_SOZ'`), 't');
  assert.equal(run(`SELECT m."IsApproved" FROM coaching.study_catalog_topic_outcomes m JOIN coaching.study_catalog_topics t ON t."Id" = m."TopicId"
    WHERE t."SourceId" = 'AGS_SOZ.1.01'`), 't');

  run(`UPDATE coaching.study_catalog_topics SET "Name" = 'Elle değiştirilmiş' WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'YDS_EN.1.01'`);
  const conflict = psql(pack('apply.sql'));
  assert.notEqual(conflict.status, 0);
  assert.match(conflict.stderr, /differs/);
  assert.equal(run(`SELECT "Name" FROM coaching.study_catalog_topics WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'YDS_EN.1.01'`), 'Elle değiştirilmiş');
});

test('stops without changing anything after the team permanently deleted one of its records', { skip }, () => {
  run(`CREATE TABLE IF NOT EXISTS coaching."AdminAuditRecords" ("Id" uuid PRIMARY KEY, "ServiceName" text, "Action" text, "ChangedFieldsJson" text);
    INSERT INTO coaching."AdminAuditRecords" VALUES ('f0ca1000-0000-4000-8000-0000000000aa', 'Coaching', 'CatalogPermanentDelete',
      '{"reason":"Yanlış konu","source":"coaching-catalog-km3","sourceId":"x"}');`);
  const lessons = run(`SELECT count(*) FROM coaching.study_catalog_lessons`);
  const result = psql(pack('apply.sql'));
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /permanently deleted 1 record/);
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons`), lessons);
  run(`DROP TABLE coaching."AdminAuditRecords";`);
});

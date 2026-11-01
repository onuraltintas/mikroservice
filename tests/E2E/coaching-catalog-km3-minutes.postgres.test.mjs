import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/coaching-catalog-km3-minutes/v1 on a disposable copy of the production coaching catalog with the
// KM3 pack applied, activated and approved as in production:
//   COACHING_CATALOG_KM3_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres \
//   COACHING_CATALOG_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 \
//   node --test tests/E2E/coaching-catalog-km3-minutes.postgres.test.mjs
const serverUrl = process.env.COACHING_CATALOG_KM3_TEST_DATABASE_URL;
const snapshot = process.env.COACHING_CATALOG_SNAPSHOT_DIR;
const skip = !(serverUrl && snapshot) && 'set COACHING_CATALOG_KM3_TEST_DATABASE_URL and COACHING_CATALOG_SNAPSHOT_DIR';
const file = (path) => readFileSync(new URL(`../../content-packs/${path}`, import.meta.url), 'utf8');
const pack = name => file(`coaching-catalog-km3-minutes/v1/${name}`);
const database = `coaching_catalog_km3_minutes_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (input, args = [], url = databaseUrl()) => spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', ...args],
  { input, encoding: 'utf8', timeout: 300000, maxBuffer: 64 * 1024 * 1024 });
const run = (input, url) => { const r = psql(input, [], url); assert.equal(r.status, 0, r.stderr || r.error?.message); return r.stdout.trim(); };
const notices = result => result.stderr.split('\n').filter(x => x.startsWith('NOTICE:')).map(x => x.slice(8).trim());
const REVIEWER = '3c1f7a52-5b8e-4f43-9f0e-2d6f6a0b9d11';
const apply = (args = ['-v', `reviewer_id=${REVIEWER}`]) => psql(pack('apply.sql'), args);
const rows = (() => { const [, ...lines] = pack('topics.csv').trim().split('\n'); return lines.map(l => { const [topic_id, outcome_id, topic_code, , old_minutes, new_minutes] = l.split(',');
  return { topic_id, outcome_id, topic_code, old_minutes: Number(old_minutes), new_minutes: Number(new_minutes) }; }); })();
const byCode = code => rows.find(x => x.topic_code === code);

// Everything the pack must leave alone: every topic, outcome and estimate other than its own rows and new versions.
const untouched = () => run(`
  SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
    SELECT t::text x FROM coaching.study_catalog_topics t WHERE t."Id" NOT IN (${rows.map(r => `'${r.topic_id}'`).join(',')})
    UNION ALL SELECT (t."Id", t."Name", t."IsActive", t."YieldWeight", t."DisplayOrder", t."UnitId", t."LessonId")::text FROM coaching.study_catalog_topics t
    UNION ALL SELECT o::text FROM coaching.study_catalog_outcomes o WHERE o."Id" NOT IN (${rows.map(r => `'${r.outcome_id}'`).join(',')})
    UNION ALL SELECT (o."Id", o."Name", o."Code", o."EditionId")::text FROM coaching.study_catalog_outcomes o
    UNION ALL SELECT e::text FROM coaching.outcome_duration_estimates e
    UNION ALL SELECT s::text FROM coaching.study_catalog_topic_outcome_shares s
    UNION ALL SELECT m::text FROM coaching.study_catalog_topic_outcomes m
    UNION ALL SELECT l::text FROM coaching.study_catalog_lessons l
    UNION ALL SELECT u::text FROM coaching.study_catalog_units u) rows;`);

let work;
before(() => {
  if (skip) return;
  work = mkdtempSync(join(tmpdir(), 'km3m-'));
  run(`DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`, serverUrl);
  const schema = readFileSync(join(snapshot, 'schema.sql'), 'utf8').split('\n')
    .filter(x => !/^\\(un)?restrict /.test(x) && !/ OWNER TO /.test(x)).join('\n');
  writeFileSync(join(work, 'schema.sql'), schema);
  // The export leaves out the duration estimates and the admin audit table; they are created as the migrations define them.
  run(`CREATE SCHEMA coaching;
    CREATE TABLE coaching.outcome_duration_estimates ("Id" uuid PRIMARY KEY, "OutcomeId" uuid NOT NULL, "Number" integer NOT NULL,
      "LearningMinutes" integer NOT NULL, "PracticeMinutes" integer NOT NULL, "CheckMinutes" integer NOT NULL,
      "Method" varchar(40) NOT NULL, "Confidence" varchar(20) NOT NULL, "Evidence" varchar(2000) NOT NULL, "ReviewedBy" uuid, "ReviewedAt" timestamptz,
      "IsPublished" boolean NOT NULL, UNIQUE ("Id", "OutcomeId"), UNIQUE ("OutcomeId", "Number"),
      CONSTRAINT ck_outcome_minutes CHECK ("LearningMinutes" >= 0 AND "PracticeMinutes" >= 0 AND "CheckMinutes" >= 0 AND ("LearningMinutes"::bigint + "PracticeMinutes" + "CheckMinutes") BETWEEN 1 AND 1440 AND "Number" > 0),
      CONSTRAINT ck_outcome_review CHECK ("Method" IN ('OfficialBudgetDraft','ReviewedStudy') AND "Confidence" IN ('Low','Medium','High') AND ("Method" <> 'ReviewedStudy' OR ("ReviewedBy" IS NOT NULL AND "ReviewedAt" IS NOT NULL)) AND (NOT "IsPublished" OR "Method" = 'ReviewedStudy')));
    CREATE TABLE coaching."AdminAuditRecords" ("Id" uuid PRIMARY KEY, "OccurredAt" timestamptz NOT NULL, "ServiceName" varchar(150) NOT NULL,
      "ActorUserId" varchar(100) NOT NULL, "ActorRoles" varchar(500) NOT NULL, "TenantId" varchar(100), "HttpMethod" varchar(10) NOT NULL,
      "Path" varchar(500) NOT NULL, "StatusCode" integer NOT NULL, "CorrelationId" varchar(100) NOT NULL, "ClientIp" varchar(64), "UserAgent" varchar(256),
      "Action" varchar(32), "ResourceType" varchar(100), "ResourceId" varchar(100), "ChangedFieldsJson" varchar(2000));
    CREATE TABLE coaching.study_plan_task_outcomes ("TaskId" uuid NOT NULL, "OutcomeId" uuid NOT NULL, "EstimateId" uuid NOT NULL,
      "AllocatedMinutes" integer NOT NULL CHECK ("AllocatedMinutes" BETWEEN 1 AND 1440), "Origin" varchar(20) NOT NULL, PRIMARY KEY ("TaskId", "OutcomeId"),
      FOREIGN KEY ("EstimateId", "OutcomeId") REFERENCES coaching.outcome_duration_estimates ("Id", "OutcomeId"));
    CREATE FUNCTION coaching.guard_catalog_exam_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN OLD; END $$;
    \\i ${join(work, 'schema.sql')}`);
  const tables = readdirSync(snapshot).filter(x => x.endsWith('.csv')).map(x => x.slice(0, -4))
    .filter(x => x !== 'study_catalog_topic_outcome_shares'); // the export leaves out the estimates these point at
  const order = ['curriculum_versions', 'study_catalog_exams', 'study_catalog_lessons', 'study_catalog_units', 'study_catalog_topics',
    'official_curriculum_editions', 'official_curriculum_topics', 'official_curriculum_lesson_bindings', 'study_catalog_outcomes',
    'study_catalog_outcome_sources', 'study_catalog_topic_outcomes', 'study_catalog_yks_scopes', 'score_models', 'target_catalog'];
  for (const table of order.filter(x => tables.includes(x))) {
    const csvFile = join(snapshot, `${table}.csv`);
    const header = readFileSync(csvFile, 'utf8').split('\n', 1)[0].split(',').map(x => `"${x}"`).join(',');
    run(`\\copy coaching.${table} (${header}) FROM '${csvFile}' WITH (FORMAT csv, HEADER true)`);
  }
  // KM1 exams, then the KM3 pack exactly as production ran it: the release in git history when available, else this
  // branch's pack rewound to the 1440-minute values (topics.csv + the previous outcome notes the KM3 guard accepts).
  run(`INSERT INTO coaching.study_catalog_exams ("Id","Code","Name","DisplayOrder","IsActive") VALUES
    ('f0ca1000-0000-4000-8000-000000000006','AGS','Akademi Giriş Sınavı',5,false),
    ('f0ca1000-0000-4000-8000-000000000007','OABT','Öğretmenlik Alan Bilgisi Testi',6,false),
    ('f0ca1000-0000-4000-8000-000000000008','YDS','Yabancı Dil Bilgisi Seviye Tespit Sınavı',7,false),
    ('f0ca1000-0000-4000-8000-000000000009','YOKDIL','Yükseköğretim Kurumları Yabancı Dil Sınavı',8,false);`);
  const released = spawnSync('git', ['show', '5056431e:content-packs/coaching-catalog-km3/apply.sql'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (released.status === 0) run(released.stdout);
  else {
    run(file('coaching-catalog-km3/apply.sql'));
    const old = JSON.parse(spawnSync(process.execPath, [new URL('../../content-packs/coaching-catalog-km3/build.mjs', import.meta.url).pathname, '--json'],
      { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }).stdout);
    for (const t of old.lessons.flatMap(l => l.topics).filter(t => t.previousMinutes !== t.minutes))
      run(`UPDATE coaching.study_catalog_topics SET "EstimatedMinutes" = ${t.previousMinutes} WHERE "Id" = '${t.topicId}';
        UPDATE coaching.study_catalog_outcomes SET "Notes" = '${t.previousOutcomeNotes.replace(/'/g, "''")}' WHERE "Id" = '${t.outcomeId}';`);
  }
  // Production (01.11.2026): KM3 lessons, units, topics and exams active, links approved, curriculum version published,
  // topic names as the panel left them (here one still has its c1ce79c name; the pack matches by id, never by name).
  run(`UPDATE coaching.study_catalog_lessons SET "IsActive" = true WHERE "Source" = 'coaching-catalog-km3';
    UPDATE coaching.study_catalog_units SET "IsActive" = true WHERE "Source" = 'coaching-catalog-km3';
    UPDATE coaching.study_catalog_topics SET "IsActive" = true WHERE "Source" = 'coaching-catalog-km3';
    UPDATE coaching.study_catalog_exams SET "IsActive" = true WHERE "Code" IN ('AGS','OABT','YDS','YOKDIL') OR "Code" LIKE 'OABT\\_%';
    UPDATE coaching.study_catalog_topic_outcomes m SET "IsApproved" = true, "ReviewedAt" = now()
      FROM coaching.study_catalog_topics t WHERE t."Id" = m."TopicId" AND t."Source" = 'coaching-catalog-km3';
    UPDATE coaching.curriculum_versions SET "IsPublished" = true WHERE "Code" = 'coaching-exam-prep-km3-2026';
    UPDATE coaching.study_catalog_topics SET "Name" = 'Erken Çocukluk Fen Eğitimi' WHERE "Source" = 'coaching-catalog-km3' AND "SourceId" = 'OABT_OKUL.2.01';`);
  // Duration versions the team may have published in the panel (KM3 itself creates none): a 1440-minute version on a
  // capped outcome with a topic share and a plan that uses it, one without a share, and one on an uncapped 1440 topic.
  const imat = byCode('OABT_IMAT.1.01'), yds = byCode('YDS_EN.6.01');
  run(`INSERT INTO coaching.outcome_duration_estimates VALUES
      ('aa000000-0000-4000-8000-000000000001', '${imat.outcome_id}', 1, 720, 540, 180, 'ReviewedStudy', 'Medium', 'Panel', '${REVIEWER}', now(), true),
      ('aa000000-0000-4000-8000-000000000002', '${yds.outcome_id}', 1, 1000, 300, 140, 'ReviewedStudy', 'Low', 'Panel', '${REVIEWER}', now(), true),
      ('aa000000-0000-4000-8000-000000000003', (SELECT "Id" FROM coaching.study_catalog_outcomes WHERE "Code" = 'OABT_BED.1.01'), 1, 1000, 340, 100, 'ReviewedStudy', 'Low', 'Panel', '${REVIEWER}', now(), true);
    INSERT INTO coaching.study_catalog_topic_outcome_shares VALUES ('${imat.topic_id}', '${imat.outcome_id}', 'aa000000-0000-4000-8000-000000000001', 720, 540, 180, 'Analiz');
    INSERT INTO coaching.study_plan_task_outcomes VALUES ('bb000000-0000-4000-8000-000000000001', '${imat.outcome_id}', 'aa000000-0000-4000-8000-000000000001', 600, 'Catalog');`);
});
after(() => {
  if (skip) return;
  run(`DROP DATABASE IF EXISTS ${database};`, serverUrl);
  rmSync(work, { recursive: true, force: true });
});

test('production state: 44 KM3 topics at 1440, 39 of them capped; check.sql lists the 39 without changing anything', { skip }, () => {
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_topics WHERE "Source" = 'coaching-catalog-km3' AND "EstimatedMinutes" = 1440`), '44');
  const before = untouched();
  const check = run(pack('check.sql'));
  assert.match(check, /km3 minutes check: 39 to change, 0 done, 0 drifted/);
  assert.match(check, /OABT_IMAT\.1\.01\|Analiz\|1440 → 3890/);
  assert.equal(untouched(), before);
});

test('the KM3 pack (4320 limit) reruns over the 1440 values production holds and changes nothing', { skip }, () => {
  const before = untouched();
  const rerun = psql(file('coaching-catalog-km3/apply.sql'));
  assert.equal(rerun.status, 0, rerun.stderr);
  assert.ok(notices(rerun).some(x => /^inserted: lessons 0, units 0, topics 0, outcomes 0/.test(x)), notices(rerun).join('\n'));
  assert.equal(untouched(), before);
});

test('refuses with nothing changed before the 4320 migration and without a reviewer', { skip }, () => {
  const before = untouched();
  const old = apply();
  assert.notEqual(old.status, 0); assert.match(old.stderr, /4320.*Nothing changed/s);
  // The migration TopicMinutesCap4320 replaces the two check constraints.
  run(`ALTER TABLE coaching.outcome_duration_estimates DROP CONSTRAINT ck_outcome_minutes, ADD CONSTRAINT ck_outcome_minutes CHECK ("LearningMinutes" >= 0 AND "PracticeMinutes" >= 0 AND "CheckMinutes" >= 0 AND ("LearningMinutes"::bigint + "PracticeMinutes" + "CheckMinutes") BETWEEN 1 AND 4320 AND "Number" > 0);
    ALTER TABLE coaching.study_catalog_topic_outcome_shares DROP CONSTRAINT ck_topic_outcome_share_minutes, ADD CONSTRAINT ck_topic_outcome_share_minutes CHECK ("LearningMinutes" >= 0 AND "PracticeMinutes" >= 0 AND "CheckMinutes" >= 0 AND ("LearningMinutes"::bigint + "PracticeMinutes" + "CheckMinutes") BETWEEN 1 AND 4320);`);
  const anonymous = apply([]);
  assert.notEqual(anonymous.status, 0); assert.match(anonymous.stderr, /reviewer_id/);
  assert.equal(untouched(), before);
});

test('refuses with nothing changed when a capped topic, its note or its duration version drifted', { skip }, () => {
  const before = untouched();
  const drift = (change, undo, pattern) => {
    run(change); const result = apply(); run(undo);
    assert.notEqual(result.status, 0, change); assert.match(result.stderr, pattern); assert.match(result.stderr, /Nothing changed/);
    assert.equal(untouched(), before, change);
  };
  const fen = byCode('OABT_FEN.1.01'), imat = byCode('OABT_IMAT.1.01');
  drift(`UPDATE coaching.study_catalog_topics SET "EstimatedMinutes" = 1200 WHERE "Id" = '${fen.topic_id}'`,
    `UPDATE coaching.study_catalog_topics SET "EstimatedMinutes" = 1440 WHERE "Id" = '${fen.topic_id}'`, /OABT_FEN\.1\.01/);
  drift(`UPDATE coaching.study_catalog_outcomes SET "Notes" = "Notes" || ' (panel)' WHERE "Id" = '${fen.outcome_id}'`,
    `UPDATE coaching.study_catalog_outcomes SET "Notes" = left("Notes", length("Notes") - 8) WHERE "Id" = '${fen.outcome_id}'`, /OABT_FEN\.1\.01/);
  drift(`UPDATE coaching.study_catalog_topics SET "Source" = 'elle' WHERE "Id" = '${fen.topic_id}'`,
    `UPDATE coaching.study_catalog_topics SET "Source" = 'coaching-catalog-km3' WHERE "Id" = '${fen.topic_id}'`, /OABT_FEN\.1\.01/);
  drift(`INSERT INTO coaching.outcome_duration_estimates VALUES ('aa000000-0000-4000-8000-000000000009', '${fen.outcome_id}', 1, 600, 200, 100, 'ReviewedStudy', 'Low', 'Panel', '${REVIEWER}', now(), true)`,
    `DELETE FROM coaching.outcome_duration_estimates WHERE "Id" = 'aa000000-0000-4000-8000-000000000009'`, /OABT_FEN\.1\.01/);
  drift(`UPDATE coaching.study_catalog_topic_outcomes SET "IsApproved" = false WHERE "TopicId" = '${imat.topic_id}'`,
    `UPDATE coaching.study_catalog_topic_outcomes SET "IsApproved" = true WHERE "TopicId" = '${imat.topic_id}'`, /OABT_IMAT\.1\.01/);
});

test('writes the uncapped minutes of the 39 topics, a new published duration version per affected outcome, audit rows; nothing else', { skip }, () => {
  const others = run(`SELECT md5(string_agg(t::text, '|' ORDER BY t."Id")) FROM coaching.study_catalog_topics t WHERE t."Id" NOT IN (${rows.map(r => `'${r.topic_id}'`).join(',')})`);
  const otherOutcomes = run(`SELECT md5(string_agg(o::text, '|' ORDER BY o."Id")) FROM coaching.study_catalog_outcomes o WHERE o."Id" NOT IN (${rows.map(r => `'${r.outcome_id}'`).join(',')})`);
  const oldEstimates = run(`SELECT md5(string_agg(e::text, '|' ORDER BY e."Id")) FROM coaching.outcome_duration_estimates e`);
  const result = apply();
  assert.equal(result.status, 0, result.stderr);
  const lines = notices(result);
  console.log(lines.join('\n'));
  assert.ok(lines.some(x => /^before: km3 topics at 1440 44, to change 39, done 0, duration versions to add 2/.test(x)), lines.join('\n'));
  assert.ok(lines.some(x => /^changed: topics 39, outcome notes 39, duration versions 2, topic shares 1, audit records 43/.test(x)), lines.join('\n'));
  assert.ok(lines.some(x => /^after: km3 topics at 1440 5, over 1440 39, max 3960/.test(x)), lines.join('\n'));
  for (const r of rows)
    assert.equal(run(`SELECT "EstimatedMinutes" FROM coaching.study_catalog_topics WHERE "Id" = '${r.topic_id}'`), String(r.new_minutes), r.topic_code);
  assert.equal(run(`SELECT "EstimatedMinutes" FROM coaching.study_catalog_topics WHERE "SourceId" = 'OABT_IMAT.1.01'`), '3890');
  assert.equal(run(`SELECT "EstimatedMinutes" FROM coaching.study_catalog_topics WHERE "SourceId" = 'OABT_IMAT.1.04'`), '1730');
  assert.equal(run(`SELECT "Name" FROM coaching.study_catalog_topics WHERE "SourceId" = 'OABT_OKUL.2.01' AND "Source" = 'coaching-catalog-km3'`), 'Erken Çocukluk Fen Eğitimi');
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_outcomes WHERE "Notes" LIKE '%planlayıcı sınırı nedeniyle 1440%'`), '0');
  assert.match(run(`SELECT "Notes" FROM coaching.study_catalog_outcomes WHERE "Code" = 'OABT_IMAT.1.01'`), /; 3890 dk\. YieldWeight/);
  // Nothing else changed: other topics and outcomes byte for byte, earlier duration versions unchanged and still published.
  assert.equal(run(`SELECT md5(string_agg(t::text, '|' ORDER BY t."Id")) FROM coaching.study_catalog_topics t WHERE t."Id" NOT IN (${rows.map(r => `'${r.topic_id}'`).join(',')})`), others);
  assert.equal(run(`SELECT md5(string_agg(o::text, '|' ORDER BY o."Id")) FROM coaching.study_catalog_outcomes o WHERE o."Id" NOT IN (${rows.map(r => `'${r.outcome_id}'`).join(',')})`), otherOutcomes);
  assert.equal(run(`SELECT md5(string_agg(e::text, '|' ORDER BY e."Id")) FROM coaching.outcome_duration_estimates e WHERE e."Id"::text LIKE 'aa000000%'`), oldEstimates);
  // New versions: the next number, ReviewedStudy by the reviewer, published, the components scaled to the new total.
  const imat = byCode('OABT_IMAT.1.01'), yds = byCode('YDS_EN.6.01');
  assert.equal(run(`SELECT string_agg("Number" || ':' || "LearningMinutes" || '+' || "PracticeMinutes" || '+' || "CheckMinutes" || ':' || "Method" || ':' || "Confidence" || ':' || "IsPublished" || ':' || ("ReviewedBy" = '${REVIEWER}'), ',' ORDER BY "Number")
    FROM coaching.outcome_duration_estimates WHERE "OutcomeId" = '${imat.outcome_id}'`), '1:720+540+180:ReviewedStudy:Medium:true:true,2:1945+1459+486:ReviewedStudy:Medium:true:true');
  assert.equal(run(`SELECT "LearningMinutes" + "PracticeMinutes" + "CheckMinutes" FROM coaching.outcome_duration_estimates WHERE "OutcomeId" = '${yds.outcome_id}' AND "Number" = 2`), '2760');
  assert.equal(run(`SELECT s."LearningMinutes" || '+' || s."PracticeMinutes" || '+' || s."CheckMinutes" || ':' || s."ScopeDescription" FROM coaching.study_catalog_topic_outcome_shares s
    JOIN coaching.outcome_duration_estimates e ON e."Id" = s."EstimateId" WHERE e."OutcomeId" = '${imat.outcome_id}' AND e."Number" = 2`), '1945+1459+486:Analiz');
  assert.equal(run(`SELECT count(*) FROM coaching.outcome_duration_estimates WHERE "OutcomeId" = (SELECT "Id" FROM coaching.study_catalog_outcomes WHERE "Code" = 'OABT_BED.1.01')`), '1');
  assert.equal(run(`SELECT "EstimateId" FROM coaching.study_plan_task_outcomes`), 'aa000000-0000-4000-8000-000000000001');
  // Audit rows in the admin panel format: one per topic, two per new duration version (created, published), by the reviewer.
  assert.equal(run(`SELECT string_agg("Action" || ':' || "ResourceType" || ':' || n, ',' ORDER BY "Action") FROM (SELECT "Action", "ResourceType", count(*) n
    FROM coaching."AdminAuditRecords" WHERE "ActorUserId" = '${REVIEWER}' AND "UserAgent" = 'content-packs/coaching-catalog-km3-minutes/v1' GROUP BY 1, 2) a`),
    'CurriculumInlineEdit:topics:39,OutcomeEstimate:StudyOutcome:2,OutcomePublish:StudyOutcome:2');
  const audit = JSON.parse(run(`SELECT "ChangedFieldsJson" FROM coaching."AdminAuditRecords" WHERE "Action" = 'CurriculumInlineEdit' AND "ResourceId" = '${imat.topic_id}'`));
  assert.deepEqual([audit.before.EstimatedMinutes, audit.after.EstimatedMinutes], [1440, 3890]);
  assert.match(audit.reason, /4320/);
  assert.match(run(pack('verify.sql')), /km3 minutes verify: ok/);
});

test('a rerun changes nothing and writes no audit rows', { skip }, () => {
  const before = untouched(); const audits = run(`SELECT count(*) FROM coaching."AdminAuditRecords"`);
  const again = apply();
  assert.equal(again.status, 0, again.stderr);
  assert.ok(notices(again).some(x => /^changed: topics 0, outcome notes 0, duration versions 0, topic shares 0, audit records 0/.test(x)), notices(again).join('\n'));
  assert.equal(untouched(), before);
  assert.equal(run(`SELECT count(*) FROM coaching."AdminAuditRecords"`), audits);
  assert.match(run(pack('check.sql')), /km3 minutes check: 0 to change, 39 done, 0 drifted/);
});

test('the KM3 pack reruns cleanly over the new minutes (and over the 1440 values before this pack)', { skip }, () => {
  const rerun = psql(file('coaching-catalog-km3/apply.sql'));
  assert.equal(rerun.status, 0, rerun.stderr);
  assert.ok(notices(rerun).some(x => /^inserted: lessons 0, units 0, topics 0, outcomes 0/.test(x)), notices(rerun).join('\n'));
  assert.match(run(file('coaching-catalog-km3/verify.sql')), /km3 verify: ok/);
});

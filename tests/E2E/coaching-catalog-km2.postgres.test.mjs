import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// Rehearses content-packs/coaching-catalog-km2/v1 (KM2, 01.11.2026) on disposable databases seeded from the production
// catalog snapshot (schema.sql + CSVs). The snapshot is never committed; point the test at a local copy:
//   KM2_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres \
//   KM2_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 node --test tests/E2E/coaching-catalog-km2.postgres.test.mjs
// The role must be allowed to create databases (each scenario clones a seeded template database).
const serverUrl = process.env.KM2_TEST_DATABASE_URL;
const snapshot = process.env.KM2_SNAPSHOT_DIR;
const skip = (!serverUrl || !snapshot) && 'set KM2_TEST_DATABASE_URL and KM2_SNAPSHOT_DIR to run against a local PostgreSQL server';
const packDir = fileURLToPath(new URL('../../content-packs/coaching-catalog-km2/v1/', import.meta.url));
const pack = name => readFileSync(join(packDir, name), 'utf8');
const template = `km2_template_${process.pid}`;
const created = [];
const urlFor = database => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (url, input, variables = {}) => spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1',
  ...Object.entries(variables).flatMap(([key, value]) => ['-v', `${key}=${value}`])], { input, encoding: 'utf8', timeout: 300000 });
const run = (url, input, variables) => {
  const result = psql(url, input, variables);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result.stdout.trim();
};
const value = (url, sql) => run(url, `SET search_path = coaching; ${sql}`);
const clone = name => {
  const database = `km2_${name}_${process.pid}`;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database} TEMPLATE ${template};`);
  created.push(database);
  return urlFor(database);
};

const AYT_TARIH = '92a96a49-051e-4121-9a8d-558f5d7945ee';
const TDP_MATEMATIK = 'e96dd842-ef0f-4056-8915-ede255cec09f';
const LGS_MATEMATIK = 'a316cd7c-7256-4180-aaea-16ca3913e511';
const REVIEWER = '00000000-0000-4000-8000-0000000000aa';
const KM2_LINK = `"Evidence" LIKE 'KM2 önerisi%'`;
const leafSql = lesson => `SELECT t."Id" FROM study_catalog_topics t WHERE t."LessonId"='${lesson}'
  AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")`;
const tables = ['curriculum_versions', 'study_catalog_exams', 'study_catalog_lessons', 'official_curriculum_editions',
  'official_curriculum_lesson_bindings', 'official_curriculum_topics', 'study_catalog_units', 'study_catalog_topics',
  'study_catalog_outcomes', 'study_catalog_outcome_sources', 'study_catalog_topic_outcomes', 'study_catalog_yks_scopes', 'target_catalog'];
// Counts of everything the stage step must leave alone (it only adds pending topic-outcome links).
const fingerprint = url => value(url, `SELECT concat_ws('|',
  (SELECT count(*) FROM study_catalog_topics), (SELECT count(*) FILTER (WHERE "IsActive") FROM study_catalog_topics),
  (SELECT md5(string_agg("Id"::text||coalesce("EstimatedMinutes",0)||"YieldWeight"||"IsActive", ',' ORDER BY "Id")) FROM study_catalog_topics),
  (SELECT count(*) FROM study_catalog_outcomes), (SELECT count(*) FROM outcome_duration_estimates),
  (SELECT count(*) FILTER (WHERE "IsPublished") FROM outcome_duration_estimates), (SELECT count(*) FROM study_catalog_topic_outcome_shares),
  (SELECT count(*) FROM study_catalog_yks_scopes), (SELECT count(*) FILTER (WHERE "IsApproved") FROM study_catalog_topic_outcomes),
  (SELECT count(*) FROM target_catalog), (SELECT count(*) FROM official_curriculum_editions));`);
// What a student would see on a topic: a simplified copy of CoachingOutcomeCatalogService.ReadPublishedAsync.
const published = (url, topicFilter) => Number(value(url, `SELECT count(*) FROM study_catalog_topic_outcomes m
  JOIN study_catalog_outcomes o ON o."Id"=m."OutcomeId" JOIN outcome_duration_estimates e ON e."OutcomeId"=o."Id" AND e."IsPublished"
   AND NOT EXISTS (SELECT 1 FROM outcome_duration_estimates n WHERE n."OutcomeId"=o."Id" AND n."IsPublished" AND n."Number">e."Number")
  LEFT JOIN study_catalog_topic_outcome_shares s ON s."TopicId"=m."TopicId" AND s."OutcomeId"=o."Id" AND s."EstimateId"=e."Id"
  WHERE m."IsApproved" AND NOT o."IsArchived" AND m."TopicId" IN (${topicFilter})
   AND (s."TopicId" IS NOT NULL OR NOT EXISTS (SELECT 1 FROM study_catalog_topic_outcome_shares x WHERE x."EstimateId"=e."Id"));`));

before(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${template}; CREATE DATABASE ${template};`);
  const url = urlFor(template);
  // schema.sql references two objects the export leaves out: the delete guard function and outcome_duration_estimates.
  run(url, `CREATE SCHEMA coaching;
    CREATE FUNCTION coaching.guard_catalog_exam_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN OLD; END $$;
    CREATE TABLE coaching.outcome_duration_estimates("Id" uuid PRIMARY KEY,"OutcomeId" uuid NOT NULL,"Number" int NOT NULL,
      "LearningMinutes" int NOT NULL,"PracticeMinutes" int NOT NULL,"CheckMinutes" int NOT NULL,"Method" varchar(40) NOT NULL,
      "Confidence" varchar(20) NOT NULL,"Evidence" varchar(2000) NOT NULL,"ReviewedBy" uuid,"ReviewedAt" timestamptz,"IsPublished" boolean NOT NULL,
      CONSTRAINT "AK_outcome_duration_estimates_Id_OutcomeId" UNIQUE("Id","OutcomeId"),
      CONSTRAINT ck_outcome_minutes CHECK ("LearningMinutes">=0 AND "PracticeMinutes">=0 AND "CheckMinutes">=0
        AND ("LearningMinutes"::bigint+"PracticeMinutes"+"CheckMinutes") BETWEEN 1 AND 1440 AND "Number">0),
      CONSTRAINT ck_outcome_review CHECK ("Method" IN ('OfficialBudgetDraft','ReviewedStudy') AND "Confidence" IN ('Low','Medium','High')
        AND ("Method"<>'ReviewedStudy' OR ("ReviewedBy" IS NOT NULL AND "ReviewedAt" IS NOT NULL)) AND (NOT "IsPublished" OR "Method"='ReviewedStudy')));
    CREATE UNIQUE INDEX "IX_outcome_duration_estimates_OutcomeId_Number" ON coaching.outcome_duration_estimates("OutcomeId","Number");`);
  const schema = readFileSync(join(snapshot, 'schema.sql'), 'utf8').split('\n')
    .filter(line => !/OWNER TO|^\\(un)?restrict/.test(line)).join('\n');
  run(url, schema);
  for (const table of tables) {
    const columns = readFileSync(join(snapshot, `${table}.csv`), 'utf8').split('\n', 1)[0].split(',').map(x => `"${x}"`).join(',');
    run(url, `\\copy coaching.${table}(${columns}) FROM '${join(snapshot, `${table}.csv`)}' WITH (FORMAT csv, HEADER true)`);
  }
  // The export has no estimate table: rebuild one published estimate per (estimate, outcome) from its topic shares.
  run(url, `CREATE TEMP TABLE shares("TopicId" uuid,"OutcomeId" uuid,"EstimateId" uuid,"LearningMinutes" int,"PracticeMinutes" int,"CheckMinutes" int,"ScopeDescription" text);
    \\copy shares FROM '${join(snapshot, 'study_catalog_topic_outcome_shares.csv')}' WITH (FORMAT csv, HEADER true)
    INSERT INTO coaching.outcome_duration_estimates SELECT "EstimateId","OutcomeId",1,sum("LearningMinutes"),sum("PracticeMinutes"),sum("CheckMinutes"),
      'ReviewedStudy','Low','snapshot rebuild','00000000-0000-4000-8000-000000000001',now(),true FROM shares GROUP BY 1,2;
    INSERT INTO coaching.study_catalog_topic_outcome_shares SELECT * FROM shares;`);
});
after(() => { if (!skip) for (const database of [...created, template]) run(serverUrl, `DROP DATABASE IF EXISTS ${database};`); });

test('the snapshot shows the gaps KM2 was asked to fill', { skip }, () => {
  const url = urlFor(template);
  assert.equal(value(url, `SELECT count(*) FROM (${leafSql(AYT_TARIH)}) x;`), '73');
  assert.equal(value(url, `SELECT count(DISTINCT m."TopicId") FROM study_catalog_topic_outcomes m WHERE m."TopicId" IN (${leafSql(AYT_TARIH)});`), '26');
  assert.equal(value(url, `SELECT count(DISTINCT "TopicId") FROM study_catalog_yks_scopes WHERE "TopicId" IN (${leafSql(AYT_TARIH)});`), '26');
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topics WHERE "LessonId"='${TDP_MATEMATIK}';`), '54');
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topics WHERE "Id" IN (${leafSql(TDP_MATEMATIK)}) AND "EstimatedMinutes" IS NULL;`), '28');
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topics WHERE "LessonId"='${LGS_MATEMATIK}';`), '19');
  assert.equal(value(url, `SELECT count(*)||'|'||count("CatalogYear")||'|'||count("PlacementYear") FROM target_catalog WHERE "CatalogType"='School';`), '3150|0|0');
});

test('stage links every AYT Tarih leaf with pending links only, changes nothing else and is idempotent', { skip }, () => {
  const url = clone('stage');
  const before = fingerprint(url);
  const output = run(url, pack('01-stage-ayt-tarih-links.sql'));
  assert.match(output, /km2 stage before: pack links 206 \| present 0 \| AYT Tarih leaves without links 47/);
  assert.match(output, /km2 stage after: inserted 206 \| pending 206 \| AYT Tarih leaves without links 0/);
  assert.equal(fingerprint(url), before, 'topics, outcomes, estimates, shares, scopes, approvals and targets are untouched');
  assert.equal(value(url, `SELECT count(*)||'|'||count(*) FILTER (WHERE NOT "IsApproved" AND "ReviewedBy" IS NULL AND "ReviewedAt" IS NULL)
    FROM study_catalog_topic_outcomes WHERE ${KM2_LINK};`), '206|206');
  assert.equal(value(url, `SELECT count(*) FROM (${leafSql(AYT_TARIH)}) l WHERE NOT EXISTS (SELECT 1 FROM study_catalog_topic_outcomes m WHERE m."TopicId"=l."Id");`), '0');
  assert.equal(value(url, `SELECT max(length("Evidence")) <= 2000 AND bool_and("Evidence" LIKE '%https://%') FROM study_catalog_topic_outcomes WHERE ${KM2_LINK};`), 't');
  assert.equal(published(url, `SELECT "TopicId" FROM study_catalog_topic_outcomes WHERE ${KM2_LINK}`), 0, 'students see nothing new');

  const again = run(url, pack('01-stage-ayt-tarih-links.sql'));
  assert.match(again, /km2 stage after: inserted 0 \| pending 206/);
  assert.equal(fingerprint(url), before);
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topic_outcomes WHERE ${KM2_LINK};`), '206');
});

test('stage refuses a catalog that drifted from the reviewed snapshot and writes nothing', { skip }, () => {
  const url = clone('drift');
  value(url, `UPDATE study_catalog_topics SET "Name"='Emeviler (değişti)' WHERE "SourceId"='topic:ebb9ce5c-b224-40cd-579f-08dc12a4d276';`);
  const result = psql(url, pack('01-stage-ayt-tarih-links.sql'));
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /km2 stage refused/);
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topic_outcomes WHERE ${KM2_LINK};`), '0');
});

test('activation waits for the team review, then publishes durations and YKS scopes only for approved links', { skip }, () => {
  const url = clone('activate');
  run(url, pack('01-stage-ayt-tarih-links.sql'));
  const missing = psql(url, pack('02-activate-ayt-tarih.sql'));
  assert.notEqual(missing.status, 0, 'reviewer_id and reason are required');

  const before = fingerprint(url);
  const idle = run(url, pack('02-activate-ayt-tarih.sql'), { reviewer_id: REVIEWER, reason: 'KM2 AYT Tarih onayı' });
  assert.match(idle, /km2 activate: reviewed topics 0 \| estimates 0 \| shares 0 \| scopes 0/);
  assert.equal(fingerprint(url), before, 'nothing is activated before the review');

  // The team reviews in the admin panel: one topic stays pending, one link is rejected, everything else is approved.
  const pendingTopic = `(SELECT "Id" FROM study_catalog_topics WHERE "SourceId"='topic:ebb9ce5c-b224-40cd-579f-08dc12a4d276')`;
  const rejectedTopic = `(SELECT "Id" FROM study_catalog_topics WHERE "SourceId"='topic:36336755-8f93-4b62-5790-08dc12a4d276')`;
  value(url, `UPDATE study_catalog_topic_outcomes SET "IsApproved"=true,"ReviewedBy"='${REVIEWER}',"ReviewedAt"=now()
      WHERE ${KM2_LINK} AND "TopicId"<>${pendingTopic};
    UPDATE study_catalog_topic_outcomes SET "IsApproved"=false WHERE ${KM2_LINK}
      AND ("TopicId","OutcomeId")=(SELECT "TopicId",min("OutcomeId"::text)::uuid FROM study_catalog_topic_outcomes WHERE ${KM2_LINK} AND "TopicId"=${rejectedTopic} GROUP BY 1);`);
  const approved = Number(value(url, `SELECT count(*) FROM study_catalog_topic_outcomes WHERE ${KM2_LINK} AND "IsApproved";`));
  const pendingLinks = Number(value(url, `SELECT count(*) FROM study_catalog_topic_outcomes WHERE ${KM2_LINK} AND "TopicId"=${pendingTopic};`));
  assert.equal(approved, 206 - pendingLinks - 1);

  const output = run(url, pack('02-activate-ayt-tarih.sql'), { reviewer_id: REVIEWER, reason: 'KM2 AYT Tarih onayı' });
  assert.match(output, new RegExp(`km2 activate: reviewed topics 46 \\| estimates ${approved} \\| shares \\d+ \\| scopes 138`));
  assert.equal(published(url, `SELECT "TopicId" FROM study_catalog_topic_outcomes WHERE ${KM2_LINK}`), approved, 'every approved link is visible');
  assert.equal(published(url, pendingTopic), 0, 'the pending topic stays passive');
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_yks_scopes WHERE "TopicId"=${pendingTopic};`), '0');
  assert.equal(value(url, `SELECT string_agg(DISTINCT "ScoreType"||':'||"TestCode", ',' ORDER BY "ScoreType"||':'||"TestCode")
    FROM study_catalog_yks_scopes WHERE "TopicId"=${rejectedTopic};`), 'EA:AYT.EDESOS1,SOZ:AYT.EDESOS1,SOZ:AYT.SOS2');
  // Each new estimate version covers exactly its approved topics and its shares add up to the estimate (OutcomeScopeBudget).
  assert.equal(value(url, `SELECT count(*) FROM outcome_duration_estimates e WHERE e."Evidence" LIKE 'KM2%' AND (
      e."LearningMinutes"<>(SELECT sum("LearningMinutes") FROM study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id")
      OR e."PracticeMinutes"<>(SELECT sum("PracticeMinutes") FROM study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id")
      OR e."CheckMinutes"<>(SELECT sum("CheckMinutes") FROM study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id")
      OR (SELECT array_agg("TopicId" ORDER BY "TopicId") FROM study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id")
       <>(SELECT array_agg(m."TopicId" ORDER BY m."TopicId") FROM study_catalog_topic_outcomes m JOIN study_catalog_topics t ON t."Id"=m."TopicId"
          WHERE m."OutcomeId"=e."OutcomeId" AND m."IsApproved" AND t."IsActive")
      OR NOT e."IsPublished" OR e."ReviewedBy"<>'${REVIEWER}');`), '0');
  // TYT students keep exactly the minutes they had.
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topic_outcome_shares s JOIN outcome_duration_estimates e ON e."Id"=s."EstimateId"
    JOIN study_catalog_topics t ON t."Id"=s."TopicId" WHERE e."Evidence" LIKE 'KM2%' AND t."LessonId"<>'${AYT_TARIH}'
    AND NOT EXISTS (SELECT 1 FROM study_catalog_topic_outcome_shares p JOIN outcome_duration_estimates pe ON pe."Id"=p."EstimateId"
      WHERE p."TopicId"=s."TopicId" AND p."OutcomeId"=s."OutcomeId" AND pe."Number"=e."Number"-1
      AND (p."LearningMinutes",p."PracticeMinutes",p."CheckMinutes")=(s."LearningMinutes",s."PracticeMinutes",s."CheckMinutes"));`), '0');

  assert.match(run(url, pack('verify.sql')), new RegExp(`km2 pending ${pendingLinks} \\| km2 approved ${approved} \\| with yks scope 72`));

  const after = fingerprint(url);
  const again = run(url, pack('02-activate-ayt-tarih.sql'), { reviewer_id: REVIEWER, reason: 'KM2 AYT Tarih onayı' });
  assert.match(again, /km2 activate: reviewed topics 0 \| estimates 0 \| shares 0 \| scopes 0/);
  assert.equal(fingerprint(url), after, 'running it twice changes nothing');
});

test('TDP Matematik estimates need an explicit approval, fill only empty leaves and are idempotent', { skip }, () => {
  const url = clone('tdp');
  assert.notEqual(psql(url, pack('03-apply-tdp-estimates.sql')).status, 0, 'approval_reason is required');
  value(url, `UPDATE study_catalog_topics SET "EstimatedMinutes"=77 WHERE "SourceId"='topic:8ac577e3-be34-40be-274a-08dce39258cc';`);
  const output = run(url, pack('03-apply-tdp-estimates.sql'), { approval_reason: 'Koçluk ekibi onayı' });
  assert.match(output, /km2 tdp: empty before 27 \| kept 1 \| updated 27 \| empty after 0/);
  assert.equal(value(url, `SELECT "EstimatedMinutes" FROM study_catalog_topics WHERE "SourceId"='topic:8ac577e3-be34-40be-274a-08dce39258cc';`), '77');
  const rows = pack('data/tdp-matematik-estimates.csv').trim().split('\n').slice(1).map(line => line.split(','));
  assert.equal(rows.length, 28);
  for (const [sourceId, minutes] of rows.filter(([id]) => id !== 'topic:8ac577e3-be34-40be-274a-08dce39258cc'))
    assert.equal(value(url, `SELECT "EstimatedMinutes" FROM study_catalog_topics WHERE "SourceId"='${sourceId}';`), minutes);
  assert.equal(value(url, `SELECT count(*) FROM study_catalog_topics WHERE "LessonId"='${TDP_MATEMATIK}' AND "YieldWeight"<>3;`), '0');
  const again = run(url, pack('03-apply-tdp-estimates.sql'), { approval_reason: 'Koçluk ekibi onayı' });
  assert.match(again, /km2 tdp: empty before 0 \| kept 28 \| updated 0 \| empty after 0/);
});

test('verify.sql and the LGS school year check are read-only and report the expected state', { skip }, () => {
  const url = clone('verify');
  const before = fingerprint(url);
  assert.match(run(url, pack('verify.sql')), /ayt tarih: leaves 73 \| linked 26 \| km2 pending 0 \| km2 approved 0 \| with yks scope 26/);
  run(url, pack('01-stage-ayt-tarih-links.sql'));
  const output = run(url, pack('verify.sql'));
  assert.match(output, /ayt tarih: leaves 73 \| linked 73 \| km2 pending 206 \| km2 approved 0 \| with yks scope 26/);
  assert.match(output, /tdp matematik: leaves 28 \| without minutes 28 \| yield weight other than 3: 0/);
  assert.match(output, /lgs matematik: official topics 12 \| outcomes 52 \| lesson periods 180 \| outcomes linked 52 \| leaves 13/);
  assert.match(output, /lgs schools: rows 3150 \| catalog year 0 \| placement year 0/);
  const year = run(url, pack('lgs-school-year-check.sql'));
  assert.match(year, /mebi-lgs\|t\|3150\|0\|0/);
  assert.match(pack('verify.sql'), /^BEGIN READ ONLY;$/m);
  assert.match(pack('lgs-school-year-check.sql'), /^BEGIN READ ONLY;$/m);
  assert.equal(fingerprint(url), before, 'stage only adds pending links; verify and the year check write nothing');
});

test('the pack data files are reproducible from the snapshot', { skip }, () => {
  const out = mkdtempSync(join(tmpdir(), 'km2-build-'));
  try {
    const result = spawnSync('python3', ['-I', join(packDir, 'build.py'), '--snapshot', snapshot, '--out', out], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    for (const name of readdirSync(join(out, 'data')))
      assert.equal(readFileSync(join(out, 'data', name), 'utf8'), pack(`data/${name}`), name);
    for (const name of ['01-stage-ayt-tarih-links.sql', '02-activate-ayt-tarih.sql', '03-apply-tdp-estimates.sql'])
      assert.equal(readFileSync(join(out, name), 'utf8'), pack(name), name);
    assert.ok(existsSync(join(out, 'data', 'ayt-tarih-links.csv')));
  } finally { rmSync(out, { recursive: true, force: true }); }
});

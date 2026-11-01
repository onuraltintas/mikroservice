import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/school-lessons-removal/v1 (KM8, 01.11.2026) on a disposable copy of the production coaching
// catalog plus synthetic student data that points at a few of the school lessons:
//   SCHOOL_LESSONS_REMOVAL_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres \
//   COACHING_CATALOG_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 \
//   node --test tests/E2E/school-lessons-removal.postgres.test.mjs
// The snapshot directory is the read-only catalog export (schema.sql + one CSV per table); it is never committed. The
// student-data tables are not in the export, so the test creates them with the production column names, foreign keys
// and exam-history triggers (Coaching migrations; Coaching.Curriculum SchoolLessonsRemovalPostgresTests runs the pack on
// the fully migrated schema).
const serverUrl = process.env.SCHOOL_LESSONS_REMOVAL_TEST_DATABASE_URL;
const snapshot = process.env.COACHING_CATALOG_SNAPSHOT_DIR;
const skip = !(serverUrl && snapshot) && 'set SCHOOL_LESSONS_REMOVAL_TEST_DATABASE_URL and COACHING_CATALOG_SNAPSHOT_DIR';
const pack = name => readFileSync(new URL(`../../content-packs/school-lessons-removal/v1/${name}`, import.meta.url), 'utf8');
const database = `school_lessons_removal_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (input, url = databaseUrl()) => spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'],
  { input, encoding: 'utf8', timeout: 300000, maxBuffer: 64 * 1024 * 1024 });
const run = (input, url) => {
  const result = psql(input, url);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result.stdout.trim();
};
const notices = result => result.stderr.split('\n').filter(x => x.startsWith('NOTICE:')).map(x => x.slice(8).trim());
const list = pack('lessons.csv').trim().split('\n').slice(1).map(x => x.split(',')[0]);
const inList = `(SELECT unnest(ARRAY[${list.map(x => `'${x}'::uuid`).join(',')}]))`;

// The lessons the synthetic student data points at (all on the list; ids from the 01.11.2026 export).
const L = {
  coursePreference: '1e3f9701-e5ab-4a5a-a7d8-93137393c621', // 5. sınıf Matematik: a course preference only
  topicState: 'ce0cca6e-1112-4830-af82-83157e9e0c39',       // 5. sınıf Matematik (nested topics): a declared topic state
  planTopic: '8187648f-9508-48a5-bc2f-67c92afedcac',        // 5. sınıf Türkçe: a study plan task on one topic
  planLesson: '78365844-5c5c-403f-9f2d-9fbce0c1deba',       // 5. sınıf İngilizce: a weekly plan task on the lesson
  weeklyDraft: 'd2ac05b0-481e-4afa-a498-1fdb9f35bc46',      // 5. sınıf Türkçe: a weekly draft slot
  examAnswers: '5da2cb0a-dd57-4b82-9d00-53f56df2fd39',      // 9. sınıf Arapça: exam lesson answers (lesson + leaf topic)
  practiceTopic: 'f141fa81-e7e4-450a-8527-46cb55b5f7e1',    // 9. sınıf Biyoloji: a practice exam topic analysis
};

// Every catalog row that is not on the list (the KEEP exam lessons and their units/topics, and everything else).
const keepRows = () => run(`
  SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
    SELECT l::text x FROM coaching.study_catalog_lessons l WHERE l."Id" NOT IN ${inList}
    UNION ALL SELECT u::text FROM coaching.study_catalog_units u WHERE u."LessonId" NOT IN ${inList}
    UNION ALL SELECT t::text FROM coaching.study_catalog_topics t WHERE t."LessonId" NOT IN ${inList}
    UNION ALL SELECT x::text FROM coaching.study_catalog_exams x
    UNION ALL SELECT c::text FROM coaching.curriculum_versions c) rows;`);
const allCatalogRows = () => run(`
  SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
    SELECT l::text x FROM coaching.study_catalog_lessons l
    UNION ALL SELECT u::text FROM coaching.study_catalog_units u
    UNION ALL SELECT t::text FROM coaching.study_catalog_topics t) rows;`);
const studentRows = () => run(`
  SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
    SELECT p::text x FROM coaching.student_course_preferences p UNION ALL SELECT s::text FROM coaching.student_topic_states s
    UNION ALL SELECT t::text FROM coaching.study_plan_tasks t UNION ALL SELECT d::text FROM coaching.study_weekly_drafts d
    UNION ALL SELECT r::text FROM coaching.exam_results r) rows;`);
const catalogCounts = () => run(`SELECT (SELECT count(*) FROM coaching.study_catalog_lessons) || '/' || (SELECT count(*) FROM coaching.study_catalog_units)
  || '/' || (SELECT count(*) FROM coaching.study_catalog_topics)`);
const listState = () => run(`SELECT count(*) FILTER (WHERE "IsActive") || '/' || count(*) FROM coaching.study_catalog_lessons WHERE "Id" IN ${inList}`);

let work;
before(() => {
  if (skip) return;
  work = mkdtempSync(join(tmpdir(), 'km8-'));
  run(`DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`, serverUrl);
  const schema = readFileSync(join(snapshot, 'schema.sql'), 'utf8').split('\n')
    .filter(x => !/^\\(un)?restrict /.test(x) && !/ OWNER TO /.test(x)).join('\n');
  writeFileSync(join(work, 'schema.sql'), schema);
  // The production exam-history guard (migration 20261002160724_GuardExamCatalogReferences) needs exam_results first.
  run(`CREATE SCHEMA coaching;
    CREATE TABLE coaching.outcome_duration_estimates ("Id" uuid NOT NULL, "OutcomeId" uuid NOT NULL, UNIQUE ("Id", "OutcomeId"));
    CREATE TABLE coaching.exam_results ("Id" uuid PRIMARY KEY, "ExamId" uuid NOT NULL, "StudentId" uuid NOT NULL,
      lesson_answers jsonb, topic_analysis jsonb, practice_exam jsonb);
    CREATE FUNCTION coaching.guard_catalog_exam_history() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        IF EXISTS (SELECT 1 FROM coaching.exam_results AS result
            CROSS JOIN LATERAL jsonb_array_elements(result.lesson_answers) AS item
            WHERE COALESCE(item->>'LessonId', item->>'lessonId')::uuid = OLD."Id"
                OR COALESCE(item->>'TopicId', item->>'topicId')::uuid = OLD."Id") THEN
            RAISE EXCEPTION 'Catalog entry is referenced by exam history' USING ERRCODE = '23503';
        END IF;
        RETURN OLD;
    END $$;
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
  // Student data (production column names and catalog foreign keys, Coaching model snapshot).
  run(`
    CREATE TABLE coaching.student_learning_contexts ("Id" uuid PRIMARY KEY, "StudentUserId" uuid NOT NULL UNIQUE, "Purpose" integer NOT NULL);
    CREATE TABLE coaching.student_course_preferences ("ContextId" uuid NOT NULL REFERENCES coaching.student_learning_contexts ("Id") ON DELETE CASCADE,
      "LessonId" uuid NOT NULL REFERENCES coaching.study_catalog_lessons ("Id") ON DELETE RESTRICT, "Priority" integer NOT NULL,
      PRIMARY KEY ("ContextId", "LessonId"));
    CREATE TABLE coaching.student_topic_states ("ContextId" uuid NOT NULL REFERENCES coaching.student_learning_contexts ("Id") ON DELETE CASCADE,
      "TopicId" uuid NOT NULL REFERENCES coaching.study_catalog_topics ("Id") ON DELETE RESTRICT, "DeclaredStatus" integer NOT NULL,
      PRIMARY KEY ("ContextId", "TopicId"));
    CREATE TABLE coaching.study_plan_tasks ("Id" uuid PRIMARY KEY, "StudentId" uuid NOT NULL, "Title" varchar(300) NOT NULL,
      "TopicId" uuid REFERENCES coaching.study_catalog_topics ("Id") ON DELETE RESTRICT, "WeeklyLessonId" uuid, "LessonNameSnapshot" varchar(300));
    CREATE TABLE coaching.study_weekly_drafts ("Id" uuid PRIMARY KEY, "StudentId" uuid NOT NULL UNIQUE, "SlotsJson" jsonb NOT NULL, "Version" integer NOT NULL);
    CREATE TABLE coaching."AdminAuditRecords" ("Id" uuid PRIMARY KEY, "OccurredAt" timestamptz NOT NULL, "ServiceName" varchar(150) NOT NULL,
      "ActorUserId" varchar(100) NOT NULL, "ActorRoles" varchar(500) NOT NULL, "TenantId" varchar(100), "HttpMethod" varchar(10) NOT NULL,
      "Path" varchar(500) NOT NULL, "StatusCode" integer NOT NULL, "CorrelationId" varchar(100) NOT NULL, "ClientIp" varchar(64),
      "UserAgent" varchar(256), "Action" varchar(32), "ResourceType" varchar(100), "ResourceId" varchar(100), "ChangedFieldsJson" varchar(2000));
    CREATE FUNCTION coaching.guard_exam_catalog_references() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE item jsonb; lesson_id uuid; topic_id uuid;
    BEGIN
        IF NEW.lesson_answers IS NULL THEN RETURN NEW; END IF;
        FOR item IN SELECT jsonb_array_elements(NEW.lesson_answers) LOOP
            lesson_id := COALESCE(item->>'LessonId', item->>'lessonId')::uuid;
            topic_id := COALESCE(item->>'TopicId', item->>'topicId')::uuid;
            PERFORM 1 FROM coaching.study_catalog_lessons WHERE "Id" = lesson_id FOR KEY SHARE;
            IF NOT FOUND THEN RAISE EXCEPTION 'Exam references a missing catalog lesson' USING ERRCODE = '23503'; END IF;
            IF topic_id IS NOT NULL THEN
                PERFORM 1 FROM coaching.study_catalog_topics WHERE "Id" = topic_id AND "LessonId" = lesson_id FOR KEY SHARE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Exam references a missing or mismatched catalog topic' USING ERRCODE = '23503'; END IF;
            END IF;
        END LOOP;
        RETURN NEW;
    END $$;
    CREATE TRIGGER guard_exam_catalog_references BEFORE INSERT OR UPDATE OF lesson_answers ON coaching.exam_results
        FOR EACH ROW EXECUTE FUNCTION coaching.guard_exam_catalog_references();`);
});
after(() => {
  if (skip) return;
  run(`DROP DATABASE IF EXISTS ${database};`, serverUrl);
  rmSync(work, { recursive: true, force: true });
});

test('the committed list is exactly the 141 exam-less lessons of the 01.11.2026 export', { skip }, () => {
  assert.equal(list.length, 141);
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons WHERE "ExamCode" IS NULL AND "Id" IN ${inList}`), '141');
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons WHERE "ExamCode" IS NULL`), '141');
  assert.equal(run(`SELECT string_agg(DISTINCT "ExamCode", ',' ORDER BY "ExamCode") FROM coaching.study_catalog_lessons`), 'AYT,LGS,TDP,TYT,YDT');
  // No outcome links, official program bindings or YKS scopes hang on them.
  assert.equal(run(`SELECT (SELECT count(*) FROM coaching.study_catalog_topic_outcomes m JOIN coaching.study_catalog_topics t ON t."Id" = m."TopicId" WHERE t."LessonId" IN ${inList})
    + (SELECT count(*) FROM coaching.study_catalog_yks_scopes s JOIN coaching.study_catalog_topics t ON t."Id" = s."TopicId" WHERE t."LessonId" IN ${inList})
    + (SELECT count(*) FROM coaching.official_curriculum_lesson_bindings b WHERE b."LessonId" IN ${inList})`), '0');
});

test('check.sql on the bare export: all 141 lessons, 618 units and 3,519 topics would be deleted', { skip }, () => {
  const before = allCatalogRows();
  const out = run(pack('check.sql'));
  assert.match(out, /^list\|ok\|141 lessons/m);
  assert.match(out, /^total\|lessons delete 141 deactivate 0\|units delete 618 deactivate 0\|topics delete 3519 deactivate 0\|student references 0$/m);
  assert.equal(out.split('\n').filter(x => x.startsWith('lesson|')).length, 141);
  assert.match(out, /^lesson\|1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d\|1\|Hayat Bilgisi\|0\|0\|0\|0\|0\|0\|0\|0\|0\|delete\|0\|0\|0\|0$/m);
  assert.equal(allCatalogRows(), before, 'check.sql is read-only');
});

test('with student data: check reports per lesson, apply deletes the unused rows and deactivates the used ones', { skip }, () => {
  // Synthetic student references (one per kind of student data that can point at the catalog).
  run(`
    INSERT INTO coaching.student_learning_contexts VALUES ('a0000000-0000-4000-8000-000000000001', 'b0000000-0000-4000-8000-000000000001', 0);
    INSERT INTO coaching.student_course_preferences VALUES ('a0000000-0000-4000-8000-000000000001', '${L.coursePreference}', 1);
    INSERT INTO coaching.student_topic_states SELECT 'a0000000-0000-4000-8000-000000000001', "Id", 1 FROM coaching.study_catalog_topics
      WHERE "LessonId" = '${L.topicState}' AND "ParentId" IS NOT NULL ORDER BY "Id" LIMIT 1;
    INSERT INTO coaching.study_plan_tasks SELECT 'c0000000-0000-4000-8000-000000000001', 'b0000000-0000-4000-8000-000000000001', 'Konu görevi', "Id", NULL, 'Türkçe'
      FROM coaching.study_catalog_topics WHERE "LessonId" = '${L.planTopic}' ORDER BY "Id" LIMIT 1;
    INSERT INTO coaching.study_plan_tasks VALUES ('c0000000-0000-4000-8000-000000000002', 'b0000000-0000-4000-8000-000000000001', 'Haftalık ders', NULL, '${L.planLesson}', 'İngilizce');
    INSERT INTO coaching.study_weekly_drafts VALUES ('d0000000-0000-4000-8000-000000000001', 'b0000000-0000-4000-8000-000000000001',
      '[{"Id":"e0000000-0000-4000-8000-000000000001","LessonId":"${L.weeklyDraft}","LessonName":"Türkçe","ExamCode":null,"Day":1,"StartMinute":900,"DurationMinutes":60}]', 1);
    INSERT INTO coaching.exam_results SELECT 'f0000000-0000-4000-8000-000000000001', gen_random_uuid(), 'b0000000-0000-4000-8000-000000000001',
      jsonb_build_array(jsonb_build_object('LessonId', '${L.examAnswers}', 'TopicId', t."Id", 'QuestionCount', 10, 'Correct', 5, 'Wrong', 3, 'Empty', 2)), NULL, NULL
      FROM coaching.study_catalog_topics t WHERE t."LessonId" = '${L.examAnswers}'
        AND NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId" = t."Id") ORDER BY t."Id" LIMIT 1;
    INSERT INTO coaching.exam_results SELECT 'f0000000-0000-4000-8000-000000000002', gen_random_uuid(), 'b0000000-0000-4000-8000-000000000001', NULL,
      jsonb_build_object('Topics', jsonb_build_array(jsonb_build_object('SectionCode', 'FEN', 'TopicId', "Id", 'Correct', 1, 'Wrong', 0, 'Empty', 0))), NULL
      FROM coaching.study_catalog_topics WHERE "LessonId" = '${L.practiceTopic}' ORDER BY "Id" LIMIT 1;`);
  const keep = keepRows(); const students = studentRows(); const full = allCatalogRows();

  const check = run(pack('check.sql'));
  assert.match(check, /^total\|lessons delete 134 deactivate 7\|units delete 614 deactivate 4\|topics delete 3513 deactivate 6\|student references 7$/m);
  // lesson|id|grade|name|units|topics|course prefs|topic states|plan topics|plan lessons|draft slots|exam answers|practice topics|action|units del|units deact|topics del|topics deact
  assert.match(check, new RegExp(`^lesson\\|${L.coursePreference}\\|5\\|Matematik\\|6\\|12\\|1\\|0\\|0\\|0\\|0\\|0\\|0\\|deactivate\\|6\\|0\\|12\\|0$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.topicState}\\|5\\|Matematik\\|7\\|24\\|0\\|1\\|0\\|0\\|0\\|0\\|0\\|deactivate\\|6\\|1\\|22\\|2$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.planTopic}\\|5\\|Türkçe\\|4\\|64\\|0\\|0\\|1\\|0\\|0\\|0\\|0\\|deactivate\\|3\\|1\\|63\\|1$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.planLesson}\\|5\\|İngilizce\\|10\\|30\\|0\\|0\\|0\\|1\\|0\\|0\\|0\\|deactivate\\|10\\|0\\|30\\|0$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.weeklyDraft}\\|5\\|Türkçe\\|6\\|34\\|0\\|0\\|0\\|0\\|1\\|0\\|0\\|deactivate\\|6\\|0\\|34\\|0$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.examAnswers}\\|9\\|Arapça\\|4\\|63\\|0\\|0\\|0\\|0\\|0\\|1\\|0\\|deactivate\\|3\\|1\\|61\\|2$`, 'm'));
  assert.match(check, new RegExp(`^lesson\\|${L.practiceTopic}\\|9\\|Biyoloji\\|2\\|19\\|0\\|0\\|0\\|0\\|0\\|0\\|1\\|deactivate\\|1\\|1\\|18\\|1$`, 'm'));
  assert.equal(allCatalogRows(), full, 'check.sql is read-only');

  const first = psql(pack('apply.sql'));
  assert.equal(first.status, 0, first.stderr);
  const n = notices(first);
  assert.ok(n.some(x => /^before: lessons 169 \(exam-less 141, active 141\), units 768, topics 5220/.test(x)), n.join('\n'));
  assert.ok(n.some(x => /^deleted: lessons 134, units 614, topics 3513/.test(x)), n.join('\n'));
  assert.ok(n.some(x => /^deactivated: lessons 7, units 4, topics 6/.test(x)), n.join('\n'));
  assert.ok(n.some(x => /^after: lessons 35 \(exam-less 7, active 0\), units 154, topics 1707/.test(x)), n.join('\n'));
  assert.equal(catalogCounts(), '35/154/1707');
  assert.equal(listState(), '0/7');
  assert.equal(run(`SELECT count(*) FROM coaching.study_catalog_lessons WHERE "ExamCode" IS NULL AND "IsActive"`), '0');
  assert.equal(run(`SELECT count(*) FILTER (WHERE "IsActive") FROM coaching.study_catalog_units WHERE "LessonId" IN ${inList}`), '0');
  assert.equal(run(`SELECT count(*) FILTER (WHERE "IsActive") FROM coaching.study_catalog_topics WHERE "LessonId" IN ${inList}`), '0');
  assert.equal(keepRows(), keep, 'the exam lessons, their units and topics, exams and curriculum versions are untouched');
  assert.equal(studentRows(), students, 'student data is untouched');
  // Every touched row is backed up with all its columns, and audited the way the admin catalog writes are.
  assert.equal(run(`SELECT (SELECT count(*) FROM coaching.removed_school_catalog_20261101_lessons) || '/' || (SELECT count(*) FROM coaching.removed_school_catalog_20261101_units)
    || '/' || (SELECT count(*) FROM coaching.removed_school_catalog_20261101_topics)`), '141/618/3519');
  assert.equal(run(`SELECT string_agg("RemovalAction" || ':' || n, ',' ORDER BY "RemovalAction") FROM (SELECT "RemovalAction", count(*) n FROM coaching.removed_school_catalog_20261101_topics GROUP BY 1) x`),
    'deactivated:6,deleted:3513');
  assert.equal(run(`SELECT count(*) FROM information_schema.columns WHERE table_schema = 'coaching' AND table_name = 'removed_school_catalog_20261101_topics'
    AND column_name IN (SELECT column_name FROM information_schema.columns WHERE table_schema = 'coaching' AND table_name = 'study_catalog_topics')`), '11');
  assert.equal(run(`SELECT string_agg("Action" || ':' || "ResourceType" || ':' || n, ',' ORDER BY "Action", "ResourceType") FROM (
    SELECT "Action", "ResourceType", count(*) n FROM coaching."AdminAuditRecords" GROUP BY 1, 2) x`),
    'CatalogPermanentDelete:Lessons:134,CatalogPermanentDelete:Topics:3513,CatalogPermanentDelete:Units:614,CatalogStatus:Lessons:7,CatalogStatus:Topics:6,CatalogStatus:Units:4');
  const audit = JSON.parse(run(`SELECT "ChangedFieldsJson" FROM coaching."AdminAuditRecords" WHERE "ResourceId" = '1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d'`));
  assert.deepEqual(Object.keys(audit).sort(), ['name', 'pack', 'reason', 'source', 'sourceId']);
  assert.equal(audit.source, 'approved-catalog'); assert.equal(audit.name, 'Hayat Bilgisi');
  assert.equal(run(`SELECT DISTINCT "ServiceName" || ':' || "ActorRoles" || ':' || "StatusCode" FROM coaching."AdminAuditRecords"`), 'Coaching:SystemAdmin:200');

  const verify = run(pack('verify.sql'));
  assert.match(verify, /school-lessons-removal verify: ok/);

  // Running it again changes nothing.
  const audits = run(`SELECT count(*) FROM coaching."AdminAuditRecords"`);
  const again = psql(pack('apply.sql'));
  assert.equal(again.status, 0, again.stderr);
  assert.ok(notices(again).some(x => /^deleted: lessons 0, units 0, topics 0/.test(x)), notices(again).join('\n'));
  assert.ok(notices(again).some(x => /^deactivated: lessons 0, units 0, topics 0/.test(x)), notices(again).join('\n'));
  assert.equal(catalogCounts(), '35/154/1707');
  assert.equal(run(`SELECT count(*) FROM coaching."AdminAuditRecords"`), audits);

  // restore.sql puts every row back exactly as it was and reactivates; a second run changes nothing.
  const restore = psql(pack('restore.sql'));
  assert.equal(restore.status, 0, restore.stderr);
  assert.ok(notices(restore).some(x => /^restored: lessons 134, units 614, topics 3513; reactivated: lessons 7, units 4, topics 6/.test(x)), notices(restore).join('\n'));
  assert.equal(allCatalogRows(), full);
  assert.equal(studentRows(), students);
  const restoreAgain = psql(pack('restore.sql'));
  assert.equal(restoreAgain.status, 0, restoreAgain.stderr);
  assert.ok(notices(restoreAgain).some(x => /^restored: lessons 0, units 0, topics 0; reactivated: lessons 0, units 0, topics 0/.test(x)), notices(restoreAgain).join('\n'));
  assert.equal(allCatalogRows(), full);

  // And it can be applied again after a restore.
  const third = psql(pack('apply.sql'));
  assert.equal(third.status, 0, third.stderr);
  assert.ok(notices(third).some(x => /^deleted: lessons 134, units 614, topics 3513/.test(x)), notices(third).join('\n'));
  assert.equal(keepRows(), keep);
  run(pack('restore.sql'));
  assert.equal(allCatalogRows(), full);
});

for (const [name, change, undo, message] of [
  ['a list lesson was renamed', `UPDATE coaching.study_catalog_lessons SET "Name" = 'Hayat Bilgisi (eski)' WHERE "Id" = '1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d'`,
    `UPDATE coaching.study_catalog_lessons SET "Name" = 'Hayat Bilgisi' WHERE "Id" = '1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d'`, /renamed/],
  ['a list lesson got an exam', `UPDATE coaching.study_catalog_lessons SET "ExamCode" = 'TYT' WHERE "Id" = '376a6a02-2fcc-4ff1-ba0e-97f6172bba2d'`,
    `UPDATE coaching.study_catalog_lessons SET "ExamCode" = NULL WHERE "Id" = '376a6a02-2fcc-4ff1-ba0e-97f6172bba2d'`, /exam code/],
  ['an exam-less lesson that is not on the list exists', `INSERT INTO coaching.study_catalog_lessons ("Id","Source","SourceId","Name","GradeNumber","ExamCode","IsActive")
    VALUES ('99999999-0000-4000-8000-000000000001', 'test', 'extra', 'Yeni okul dersi', 10, NULL, true)`,
    `DELETE FROM coaching.study_catalog_lessons WHERE "Id" = '99999999-0000-4000-8000-000000000001'`, /not on the list/],
  ['a list lesson is missing without a backup', `CREATE TABLE public.km8_saved AS SELECT * FROM coaching.study_catalog_lessons WHERE "Id" = '1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d';
    DELETE FROM coaching.study_catalog_lessons WHERE "Id" = '1f1ac0c6-91ad-4ed8-ab3e-6220de287d1d'`,
    `INSERT INTO coaching.study_catalog_lessons SELECT * FROM public.km8_saved; DROP TABLE public.km8_saved`, /missing/],
  ['a list topic got an outcome link', `INSERT INTO coaching.study_catalog_yks_scopes ("TopicId","ScoreType","TestCode","ExamCode","ForeignLanguage","Evidence","ExcludedOutcomeIds")
    SELECT "Id", 'SAY', 'TYT.MAT', 'TYT', NULL, 'test', '{}' FROM coaching.study_catalog_topics WHERE "LessonId" = '${L.planLesson}' ORDER BY "Id" LIMIT 1`,
    `DELETE FROM coaching.study_catalog_yks_scopes s USING coaching.study_catalog_topics t WHERE t."Id" = s."TopicId" AND t."LessonId" = '${L.planLesson}'`, /outcome links, program bindings or YKS scopes/],
]) {
  test(`apply.sql refuses and changes nothing when ${name}`, { skip }, () => {
    const backups = run(`SELECT coalesce(to_regclass('coaching.removed_school_catalog_20261101_lessons')::text, '-')`);
    const lessons = backups === '-' ? '-' : run(`SELECT count(*) FROM coaching.removed_school_catalog_20261101_lessons`);
    run(change);
    const state = allCatalogRows();
    const audits = run(`SELECT count(*) FROM coaching."AdminAuditRecords"`);
    const result = psql(pack('apply.sql'));
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, message);
    assert.match(result.stderr, /Nothing changed/);
    assert.equal(allCatalogRows(), state);
    assert.equal(run(`SELECT count(*) FROM coaching."AdminAuditRecords"`), audits);
    assert.equal(backups === '-' ? '-' : run(`SELECT count(*) FROM coaching.removed_school_catalog_20261101_lessons`), lessons);
    // check.sql names the same drift instead of failing.
    assert.match(run(pack('check.sql')), /^list\|drift\|/m);
    run(undo);
  });
}

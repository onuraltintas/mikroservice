import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

// Rehearses programs/v6 (daily blocks and weekly weighting, decision 30.10.2026) on a disposable database of a local
// PostgreSQL server:
//   PROGRAMS_V6_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres node --test tests/E2E/program-blocks-v6.postgres.test.mjs
const serverUrl = process.env.PROGRAMS_V6_TEST_DATABASE_URL;
const skip = !serverUrl && 'set PROGRAMS_V6_TEST_DATABASE_URL to run against a local PostgreSQL server';
const pack = new URL('../../content-packs/programs/v6/', import.meta.url);
const plans = JSON.parse(readFileSync(new URL('programs.json', pack), 'utf8'));
const database = `programs_v6_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const run = (url, input, success = true) => {
  const result = spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'], { input, encoding: 'utf8', timeout: 120000 });
  if (success) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
};
const sql = (input, success = true) => run(databaseUrl(), input, success);
const file = name => readFileSync(new URL(name, pack), 'utf8');
let work;

before(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`);
  work = mkdtempSync(path.join(tmpdir(), 'programs-v6-'));
});
after(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database};`);
  rmSync(work, { recursive: true, force: true });
});

const uuid = (prefix, index) => `${prefix}-0000-0000-0000-${String(index).padStart(12, '0')}`;

function seed() {
  const types = new Set(['Focus', 'TargetHunt']);
  for (const plan of plans) for (const pattern of [plan.previousPattern, plan.pattern]) for (const [week, days] of Object.entries(pattern))
    if (week.startsWith('week')) for (const slots of Object.values(days)) for (const slot of slots) types.add(slot.type);
  const ages = [...new Set(plans.map(plan => plan.ageId))];
  const typeRows = [...types].map((name, index) => `('${uuid('30000000', index + 1)}','${name}')`).join(',');
  const exerciseRows = [];
  let next = 0;
  [...types].forEach((name, typeIndex) => {
    for (const age of ages) for (let level = 1; level <= 5; level++) {
      const modes = name === 'Comprehension' ? ['learning', 'evaluation'] : [null];
      for (const mode of modes) {
        const config = mode ? { engineConfig: { comprehensionMode: mode } } : {};
        exerciseRows.push(`('${uuid('20000000', ++next)}','${uuid('30000000', typeIndex + 1)}','${age}',${level},'${JSON.stringify(config)}')`);
      }
    }
  });
  const templates = plans.map(plan => `('${plan.id}','${plan.ageId}',${plan.level},$p$${JSON.stringify(plan.previousPattern)}$p$::jsonb,$d$${plan.previousDescription}$d$)`).join(',');
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.exercise_types(id,name) VALUES ${typeRows};
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,difficulty_level int,
      configuration_json jsonb,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,is_assessment_only boolean DEFAULT false);
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json) VALUES ${exerciseRows.join(',')};
    CREATE TABLE speed_reading.program_templates(id uuid PRIMARY KEY,"TargetAgeGroupConfigurationId" uuid,"InitialDifficultyLevel" int,
      "WeeklyPatternJson" jsonb,"Description" varchar(4000),"IsActive" boolean DEFAULT true,"IsDeleted" boolean DEFAULT false,"IsAssessment" boolean DEFAULT false,
      "TotalDays" int DEFAULT 28,"TotalWeeks" int DEFAULT 4,version int DEFAULT 3,updated_at timestamptz DEFAULT '2026-10-13T00:00:00Z',
      updated_by text DEFAULT 'system:program-target-hunt-v5');
    INSERT INTO speed_reading.program_templates(id,"TargetAgeGroupConfigurationId","InitialDifficultyLevel","WeeklyPatternJson","Description") VALUES ${templates};
    CREATE TABLE speed_reading.student_program_progress(id uuid PRIMARY KEY,"UserId" uuid,"ProgramTemplateId" uuid,"AssignedDate" timestamptz,
      "ScheduleJson" jsonb,"CurrentWeek" int,"CurrentDay" int,"DaysCompleted" int,"ExercisesCompleted" int,"IsActive" boolean DEFAULT true,
      "CompletedDate" timestamptz,version int DEFAULT 2,updated_at timestamptz DEFAULT '2026-10-20T00:00:00Z',updated_by text DEFAULT 'student');
    CREATE TABLE speed_reading.daily_exercise_logs(id uuid PRIMARY KEY,"StudentProgramProgressId" uuid,"WeekNumber" int,"DayNumber" int,"ExerciseId" uuid);
    CREATE TABLE speed_reading.exercise_sessions(id uuid PRIMARY KEY,student_id uuid,assessment_attempt_id uuid,start_time timestamptz);`);
  // One active assignment on day 2: day 1 is finished, days 2–3 are still open.
  const plan = plans[0];
  const pick = type => JSON.parse(sql(`SELECT to_jsonb(e.id) FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE t.name='${type}' AND e.target_age_group_id='${plan.ageId}' AND e.difficulty_level=${plan.level} ORDER BY e.id LIMIT 1;`).stdout);
  const focus = pick('Focus'), eye = pick('EyeTracking');
  const schedule = [1, 2, 3].flatMap(day => [eye, focus].map((id, index) => ({ WeekNumber: 1, DayNumber: day, Order: index + 1, ExerciseId: id })));
  sql(`INSERT INTO speed_reading.student_program_progress VALUES('${uuid('99999999', 1)}','${uuid('88888888', 1)}','${plan.id}','2026-10-19T00:00:00Z',
      $s$${JSON.stringify(schedule)}$s$,1,2,1,2,true,NULL,2,'2026-10-20T00:00:00Z','student');
    INSERT INTO speed_reading.daily_exercise_logs VALUES('${uuid('77777777', 1)}','${uuid('99999999', 1)}',1,1,'${focus}');`);
  return { focus, eye, plan };
}

test('applies, verifies, is idempotent and rolls back exactly', { skip }, () => {
  const { focus, eye, plan } = seed();
  const inventory = () => sql(`SELECT md5(string_agg(md5(t::text),'' ORDER BY t::text)) FROM (
    SELECT to_jsonb(p) t FROM speed_reading.program_templates p UNION ALL SELECT to_jsonb(s) FROM speed_reading.student_program_progress s
    UNION ALL SELECT to_jsonb(l) FROM speed_reading.daily_exercise_logs l) rows;`).stdout;
  const beforeInventory = inventory();
  writeFileSync(path.join(work, 'snapshot.json'), sql(file('snapshot.sql')).stdout);
  const prepared = spawnSync(process.execPath, [new URL('prepare-release.mjs', pack).pathname, path.join(work, 'snapshot.json'), path.join(work, 'release')],
    { encoding: 'utf8' });
  assert.equal(prepared.status, 0, prepared.stderr);

  sql(readFileSync(path.join(work, 'release', 'apply.sql'), 'utf8'));
  assert.match(sql(file('verify.sql')).stdout, /PASS/);
  writeFileSync(path.join(work, 'after.json'), sql(file('snapshot.sql')).stdout);
  const verified = spawnSync(process.execPath, [new URL('verify-release.mjs', pack).pathname, path.join(work, 'snapshot.json'), path.join(work, 'after.json')],
    { encoding: 'utf8' });
  assert.equal(verified.status, 0, verified.stderr);

  // Day 1 is finished and kept; days 2–3 are rebuilt as v6 days (6 tasks each, in block order) at the program level.
  const schedule = JSON.parse(sql(`SELECT "ScheduleJson" FROM speed_reading.student_program_progress;`).stdout);
  const types = new Map(sql(`SELECT e.id||'|'||t.name||'|'||e.difficulty_level FROM speed_reading.exercises e
    JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id;`).stdout.trim().split('\n').map(row => row.split('|')).map(([id, type, level]) => [id, { type, level: +level }]));
  const dayOf = number => schedule.filter(slot => slot.WeekNumber === 1 && slot.DayNumber === number).sort((a, b) => a.Order - b.Order);
  assert.deepEqual(dayOf(1).map(slot => slot.ExerciseId), [eye, focus]);
  for (const number of [2, 3]) {
    assert.deepEqual(dayOf(number).map(slot => types.get(slot.ExerciseId).type), plan.pattern.week1[`day${number}`].map(slot => slot.type));
    assert.ok(dayOf(number).every(slot => types.get(slot.ExerciseId).level === plan.level));
  }
  assert.equal(sql(`SELECT count(*) FROM speed_reading.program_templates WHERE "WeeklyPatternJson"::text LIKE '%"Focus"%';`).stdout.trim(), '0');
  assert.equal(sql(`SELECT count(*) FROM speed_reading.program_templates WHERE "Description" LIKE '%gruplama%';`).stdout.trim(), '20');

  const afterFirst = inventory();
  sql(readFileSync(path.join(work, 'release', 'apply.sql'), 'utf8'));
  assert.equal(inventory(), afterFirst);
  sql(readFileSync(path.join(work, 'release', 'rollback.sql'), 'utf8'));
  assert.equal(inventory(), beforeInventory);
});

test('stops when training activity changed after the snapshot', { skip }, () => {
  seed();
  writeFileSync(path.join(work, 'snapshot.json'), sql(file('snapshot.sql')).stdout);
  const prepared = spawnSync(process.execPath, [new URL('prepare-release.mjs', pack).pathname, path.join(work, 'snapshot.json'), path.join(work, 'release2')],
    { encoding: 'utf8' });
  assert.equal(prepared.status, 0, prepared.stderr);
  sql(`INSERT INTO speed_reading.daily_exercise_logs VALUES('${uuid('77777777', 2)}','${uuid('99999999', 1)}',1,2,'${uuid('20000000', 1)}');`);
  const failed = sql(readFileSync(path.join(work, 'release2', 'apply.sql'), 'utf8'), false);
  assert.notEqual(failed.status, 0); assert.match(failed.stderr, /fresh snapshot/);
});

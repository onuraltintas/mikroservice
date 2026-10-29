import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

// Runs against a disposable database on a local PostgreSQL server:
//   TARGET_HUNT_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres node --test tests/E2E/target-hunt-catalog.postgres.test.mjs
const serverUrl = process.env.TARGET_HUNT_TEST_DATABASE_URL;
const settings = JSON.parse(readFileSync(new URL('../../content-packs/target-hunt/v1/settings.json', import.meta.url), 'utf8'));
const apply = readFileSync(new URL('../../content-packs/target-hunt/v1/apply.sql', import.meta.url), 'utf8');
const database = `target_hunt_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (url, input, success = true) => {
  const result = spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'], { input, encoding: 'utf8', timeout: 60000 });
  if (success) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
};
const sql = (input, success = true) => psql(databaseUrl(), input, success);
const skip = !serverUrl && 'set TARGET_HUNT_TEST_DATABASE_URL to run against a local PostgreSQL server';

before(() => { if (!skip) psql(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`); });
after(() => { if (!skip) psql(serverUrl, `DROP DATABASE IF EXISTS ${database};`); });

function seed({ category = true, existingType = null } = {}) {
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_type_categories(id uuid PRIMARY KEY,name text NOT NULL,is_deleted boolean DEFAULT false);
    ${category ? `INSERT INTO speed_reading.exercise_type_categories VALUES('40000000-0000-0000-0000-000000000001','attention-and-eye-movement',false);` : ''}
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name varchar(100) NOT NULL,display_name varchar(200) NOT NULL,
      description varchar(2000) NOT NULL,icon_name varchar(100) NOT NULL,color_code varchar(30) NOT NULL,sort_order int NOT NULL,
      is_active boolean NOT NULL,engine_type varchar(100) NOT NULL,category_id uuid,created_at timestamptz NOT NULL,updated_at timestamptz,
      created_by varchar(100),updated_by varchar(100),version int NOT NULL,is_deleted boolean NOT NULL DEFAULT false,deleted_at timestamptz,deleted_by varchar(100));
    INSERT INTO speed_reading.exercise_types(id,name,display_name,description,icon_name,color_code,sort_order,is_active,engine_type,created_at,version)
      VALUES('30000000-0000-0000-0000-000000000001','Focus','Odaklanma','','psychology','#f57c00',1,true,'focus',now(),1);
    ${existingType ? `INSERT INTO speed_reading.exercise_types(id,name,display_name,description,icon_name,color_code,sort_order,is_active,engine_type,created_at,version)
      VALUES('${existingType.id}','TargetHunt','Hedef Avı','','search','#2e7d32',1,true,'${existingType.engine}',now(),1);` : ''}
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid NOT NULL,title text NOT NULL,description text NOT NULL,
      type_code text,difficulty_level int NOT NULL,configuration_json jsonb NOT NULL,target_age_group_id uuid,creator_id uuid,
      is_active boolean NOT NULL,is_assessment_only boolean NOT NULL DEFAULT false,created_at timestamptz NOT NULL,updated_at timestamptz,
      created_by varchar(100),updated_by varchar(100),version int NOT NULL,is_deleted boolean NOT NULL DEFAULT false,deleted_at timestamptz,deleted_by varchar(100));`);
}
const snapshot = () => sql(`SELECT jsonb_build_object('types',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM speed_reading.exercise_types t),
  'exercises',(SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e));`).stdout;

test('creates the attention type and twenty exercises, and a second run changes nothing', { skip }, () => {
  seed(); sql(apply);
  const type = JSON.parse(sql(`SELECT to_jsonb(t) FROM speed_reading.exercise_types t WHERE name='TargetHunt';`).stdout);
  assert.equal(type.id, settings.type.id); assert.equal(type.engine_type, 'target_hunt');
  assert.equal(type.category_id, '40000000-0000-0000-0000-000000000001');
  const rows = JSON.parse(sql(`SELECT jsonb_agg(jsonb_build_object('id',id,'age',target_age_group_id,'level',difficulty_level,
    'config',configuration_json,'type',exercise_type_id) ORDER BY id) FROM speed_reading.exercises;`).stdout);
  assert.equal(rows.length, 20);
  for (const row of rows) {
    const expected = settings.exercises.find(item => item.id === row.id);
    assert.ok(expected); assert.equal(row.age, expected.ageGroupId); assert.equal(row.level, expected.level);
    assert.equal(row.type, settings.type.id);
    assert.deepEqual(row.config.engineConfig, expected.configuration.engineConfig);
  }
  const first = snapshot(); sql(apply); assert.equal(snapshot(), first);
});

test('refuses to reuse a TargetHunt type that runs another engine', { skip }, () => {
  seed({ existingType: { id: '30000000-0000-0000-0000-000000000009', engine: 'grid_interaction' } });
  const before = snapshot(); const failed = sql(apply, false);
  assert.notEqual(failed.status, 0); assert.match(failed.stderr, /TargetHunt/); assert.equal(snapshot(), before);
});

test('refuses to run without the attention category', { skip }, () => {
  seed({ category: false });
  const before = snapshot(); const failed = sql(apply, false);
  assert.notEqual(failed.status, 0); assert.match(failed.stderr, /attention/); assert.equal(snapshot(), before);
});

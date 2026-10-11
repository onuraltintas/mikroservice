import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const settings = JSON.parse(readFileSync(new URL('../../content-packs/saccade/v1/settings.json', import.meta.url)));
const apply = readFileSync(new URL('../../content-packs/saccade/v1/apply.sql', import.meta.url), 'utf8');
const template = readFileSync(new URL('../../content-packs/saccade/v1/apply.template.sql', import.meta.url), 'utf8');
const typeId = '30000000-0000-0000-0000-000000000001';
let container;
const docker = args => spawnSync('docker', args, { encoding: 'utf8', timeout: 60000 });
const sql = (input, success = true) => {
  const result = spawnSync('docker', ['exec', '-i', container, 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres'],
    { input, encoding: 'utf8', timeout: 60000 });
  if (success) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
};
before(async () => {
  const started = docker(['run', '-d', '--name', `saccade-test-${process.pid}-${Date.now()}`,
    '-e', 'POSTGRES_HOST_AUTH_METHOD=trust', 'postgres:16-alpine']);
  assert.equal(started.status, 0, started.stderr || started.error?.message);
  container = started.stdout.trim(); assert.match(container, /^[0-9a-f]{64}$/);
  for (let attempt = 0; attempt < 150; attempt++) {
    if (docker(['exec', container, 'pg_isready', '-U', 'postgres']).status === 0) return;
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error('Disposable PostgreSQL did not become ready');
});
after(() => { if (container) assert.equal(docker(['rm', '-fv', container]).status, 0); });

function seed() {
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,engine_type text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.exercise_types VALUES('${typeId}','Saccade','motion_path',true,false);
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,difficulty_level int,
      configuration_json jsonb,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,version int DEFAULT 1,
      updated_at timestamptz,updated_by text);
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json)
      SELECT (s->>'id')::uuid,'${typeId}',(s->>'ageGroupId')::uuid,(s->>'level')::int,s->'previousConfiguration'
      FROM jsonb_array_elements($settings$${JSON.stringify(settings)}$settings$::jsonb) s;
    INSERT INTO speed_reading.exercises VALUES('99999999-0000-0000-0000-000000000001','${typeId}',NULL,1,'{"assessment":true}',true,false,1,NULL,NULL);
    CREATE TABLE speed_reading.session_snapshots(id int,configuration_json jsonb);
    INSERT INTO speed_reading.session_snapshots VALUES(1,$snapshot$${JSON.stringify(settings[0].previousConfiguration)}$snapshot$::jsonb);`);
}
const rows = () => JSON.parse(sql('SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e;').stdout);
function rejected(mutation) {
  seed(); sql(mutation); const before = rows(); const failed = sql(apply, false);
  assert.equal(failed.status, 3, failed.stderr); assert.match(failed.stderr, /Saccade/);
  assert.deepEqual(rows(), before);
}
test('rendered SQL matches the template and approved settings', () => {
  assert.equal(apply, template.replace('{{SETTINGS}}', JSON.stringify(settings, null, 2)));
});
test('updates exactly twenty records and preserves the assessment and open session snapshot', () => {
  seed(); const before = rows(); const snapshot = sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout;
  sql(apply); const after = rows();
  for (let index = 0; index < before.length; index++) {
    const patch = settings.find(item => item.id === before[index].id);
    assert.deepEqual(after[index], patch ? { ...before[index], configuration_json: patch.configuration,
      version: before[index].version + 1, updated_by: 'saccade-v1', updated_at: after[index].updated_at } : before[index]);
  }
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout, snapshot);
});
test('second application preserves timestamps and versions', () => { seed(); sql(apply); const first = rows(); sql(apply); assert.deepEqual(rows(), first); });
test('changed settings roll back the entire transaction', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=configuration_json||'{"HoldMs":123}' WHERE id='${settings[0].id}';`));
test('missing record rolls back', () => rejected(`DELETE FROM speed_reading.exercises WHERE id='${settings[0].id}';`));
test('inactive record rolls back', () => rejected(`UPDATE speed_reading.exercises SET is_active=false WHERE id='${settings[0].id}';`));
test('wrong age rolls back', () => rejected(`UPDATE speed_reading.exercises SET target_age_group_id=NULL WHERE id='${settings[0].id}';`));
test('wrong level rolls back', () => rejected(`UPDATE speed_reading.exercises SET difficulty_level=5 WHERE id='${settings[0].id}';`));
test('wrong engine rolls back', () => rejected("UPDATE speed_reading.exercise_types SET engine_type='text_stream';"));
test('duplicate training exercise rolls back', () => rejected(`UPDATE speed_reading.exercises SET target_age_group_id='${settings[0].ageGroupId}' WHERE id='99999999-0000-0000-0000-000000000001';`));

for (const [name, mutate] of [
  ['target mode', config => { config.engineConfig.mode = 'tracking'; }],
  ['unapproved non-timing field', config => { config.engineConfig.content.pointSize = 99; }],
  ['missing duration', config => { delete config.engineConfig.timing.durationSeconds; }],
  ['null interval', config => { config.engineConfig.timing.holdMs = null; }],
  ['non-numeric duration', config => { config.engineConfig.timing.durationSeconds = '60'; }]
]) {
  test(`rejects payload drift in ${name} without modifying records`, () => {
    seed(); const before = rows(); const changed = structuredClone(settings);
    mutate(changed[0].configuration);
    const failed = sql(template.replace('{{SETTINGS}}', JSON.stringify(changed)), false);
    assert.equal(failed.status, 3, failed.stderr);
    assert.match(failed.stderr, /Saccade/);
    assert.deepEqual(rows(), before);
  });
}

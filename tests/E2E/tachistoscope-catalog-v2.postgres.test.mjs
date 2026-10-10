import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const patches = JSON.parse(readFileSync(new URL('../../content-packs/tachistoscope-settings/v2/settings.json', import.meta.url), 'utf8'));
const apply = readFileSync(new URL('../../content-packs/tachistoscope-settings/v2/apply.sql', import.meta.url), 'utf8');
const fixtures = patches.map(patch => ({ id: patch.id, target_age_group_id: patch.ageGroupId,
  difficulty_level: patch.level, type_code: 'Tachistoscope', title: 'Kısa Gösterimde Tanıma',
  configuration_json: patch.previousConfiguration }));
for (let age = 1; age <= 4; age++) fixtures.push({ ...fixtures[0],
  id: `99999999-0000-0000-0000-00000000000${age}`, title: 'Seviye Tespit — Tachistoscope',
  configuration_json: { engineType: 'text_stream', engineConfig: { content: { type: 'number', count: 6 }, adaptive: { enabled: false } } } });
fixtures.push({ ...fixtures[0], id: '99999999-0000-0000-0000-000000000005', type_code: 'Focus' });
let container;
const docker = args => spawnSync('docker', args, { encoding: 'utf8', timeout: 60000 });
function sql(input, mustPass = true) {
  const result = spawnSync('docker', ['exec', '-i', container, 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres'],
    { input, encoding: 'utf8', timeout: 60000 });
  if (mustPass) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
}
before(async () => {
  const result = docker(['run', '-d', '--name', `tachistoscope-v2-test-${process.pid}-${Date.now()}`,
    '-e', 'POSTGRES_HOST_AUTH_METHOD=trust', 'postgres:16-alpine']);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  container = result.stdout.trim();
  assert.match(container, /^[0-9a-f]{64}$/);
  for (let attempt = 0; attempt < 150; attempt++) {
    if (docker(['exec', container, 'pg_isready', '-U', 'postgres']).status === 0) return;
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error('Disposable PostgreSQL did not become ready');
});
after(() => {
  if (container) assert.equal(docker(['rm', '-fv', container]).status, 0, 'Disposable database and its volumes must be removed');
});
function seed() {
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,target_age_group_id uuid,difficulty_level int,
      type_code text,title text,configuration_json jsonb,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,
      version int DEFAULT 1,created_at timestamptz DEFAULT '2026-01-01Z',created_by text DEFAULT 'original',
      updated_at timestamptz,updated_by text);
    INSERT INTO speed_reading.exercises(id,target_age_group_id,difficulty_level,type_code,title,configuration_json)
      SELECT id,target_age_group_id,difficulty_level,type_code,title,configuration_json
      FROM jsonb_to_recordset($fixtures$${JSON.stringify(fixtures)}$fixtures$::jsonb)
      AS t(id uuid,target_age_group_id uuid,difficulty_level int,type_code text,title text,configuration_json jsonb);
    CREATE TABLE speed_reading.session_snapshots(id int,configuration_json jsonb);
    INSERT INTO speed_reading.session_snapshots VALUES(1,$snapshot$${JSON.stringify(fixtures[0].configuration_json)}$snapshot$::jsonb);`);
}
const rows = () => JSON.parse(sql('SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e;').stdout);
function rejected(mutation) {
  seed(); sql(mutation); const before = rows();
  const result = sql(apply, false);
  assert.equal(result.status, 3, result.stderr || result.error?.message);
  assert.match(result.stderr, /ERROR:.*(Tachistoscope|Unexpected duplicate)/);
  assert.deepEqual(rows(), before, 'Rejected update must roll back every row');
}
test('updates twenty training records without changing assessment, other exercises or session snapshots', () => {
  seed(); const before = rows(); const snapshot = sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout;
  sql(apply); const after = rows();
  assert.equal(after.length, before.length);
  for (let i = 0; i < before.length; i++) {
    const patch = patches.find(patch => patch.id === before[i].id);
    if (!patch) { assert.deepEqual(after[i], before[i]); continue; }
    const expected = { ...before[i], configuration_json: patch.configuration, version: before[i].version + 1,
      updated_by: 'system:tachistoscope-settings-v2', updated_at: after[i].updated_at };
    assert.ok(expected.updated_at);
    assert.deepEqual(after[i], expected);
  }
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout, snapshot);
});
test('second application is a no-op including timestamps and versions', () => {
  seed(); sql(apply); const first = rows(); sql(apply); assert.deepEqual(rows(), first);
});
test('missing record aborts', () => rejected(`DELETE FROM speed_reading.exercises WHERE id='${patches[0].id}';`));
test('wrong age aborts', () => rejected(`UPDATE speed_reading.exercises SET target_age_group_id='99999999-0000-0000-0000-000000000006' WHERE id='${patches[0].id}';`));
test('wrong level aborts', () => rejected(`UPDATE speed_reading.exercises SET difficulty_level=5 WHERE id='${patches[0].id}';`));
test('changed duration aborts rather than overwriting administrator edits', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=jsonb_set(configuration_json,'{engineConfig,timing,durationMs}','777') WHERE id='${patches[0].id}';`));
test('missing source configuration aborts', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=NULL WHERE id='${patches[0].id}';`));
test('extra alias aborts', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=configuration_json||'{"TotalStimuli":99}'::jsonb WHERE id='${patches[0].id}';`));
test('duplicate training exercise aborts', () => rejected("UPDATE speed_reading.exercises SET type_code='Tachistoscope' WHERE id='99999999-0000-0000-0000-000000000005';"));
test('inactive source aborts', () => rejected(`UPDATE speed_reading.exercises SET is_active=false WHERE id='${patches[0].id}';`));

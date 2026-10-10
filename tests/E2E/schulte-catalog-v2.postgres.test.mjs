import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const patches = JSON.parse(readFileSync(new URL('../../content-packs/schulte-settings/v2/settings.json', import.meta.url), 'utf8'));
const apply = readFileSync(new URL('../../content-packs/schulte-settings/v2/apply.sql', import.meta.url), 'utf8');
const folders = { Child: 'child', Teen: 'teen', YoungAdult: 'young-adult', Adult: 'adult' };
const fixtures = patches.map(patch => {
  const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${folders[patch.age]}-exercises/v1/catalog.json`, import.meta.url), 'utf8'));
  const old = catalog.find(row => row.id === patch.id);
  return { id: old.id, target_age_group_id: old.targetAgeGroupId, difficulty_level: old.difficultyLevel,
    type_code: old.type, title: old.title, configuration_json: { ...old.configuration, preservedOption: false } };
});
fixtures.push({ ...fixtures[0], id: '99999999-0000-0000-0000-000000000001', type_code: 'Focus' });
let container;
const docker = args => spawnSync('docker', args, { encoding: 'utf8', timeout: 60000 });
function sql(input, mustPass = true) {
  const result = spawnSync('docker', ['exec', '-i', container, 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres'],
    { input, encoding: 'utf8', timeout: 60000 });
  if (mustPass) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
}
before(async () => {
  const result = docker(['run', '-d', '--name', `schulte-v2-test-${process.pid}-${Date.now()}`,
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
  if (container) {
    const result = docker(['rm', '-f', container]);
    assert.equal(result.status, 0, result.stderr);
  }
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
const rows = () => JSON.parse(sql("SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e;").stdout);
function rejected(mutation) {
  seed(); sql(mutation);
  const before = rows();
  assert.notEqual(sql(apply, false).status, 0);
  assert.deepEqual(rows(), before, 'Failed patch must roll back every row');
}

test('updates exactly twenty configurations, preserves other fields and existing session snapshots', () => {
  seed();
  const before = rows();
  const snapshot = sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout;
  sql(apply);
  const after = rows();
  assert.equal(after.length, before.length);
  for (let index = 0; index < before.length; index++) {
    const patch = patches.find(patch => patch.id === before[index].id);
    if (!patch) { assert.deepEqual(after[index], before[index]); continue; }
    const expected = structuredClone(before[index]);
    expected.configuration_json.engineConfig.gridSize = patch.gridSize;
    expected.configuration_json.engineConfig.rules.timeLimit = patch.timeLimit;
    expected.version++;
    expected.updated_by = 'system:schulte-settings-v2';
    expected.updated_at = after[index].updated_at;
    assert.ok(expected.updated_at);
    assert.deepEqual(after[index], expected);
  }
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots;').stdout, snapshot);
});
test('reapplying is a no-op including version and audit timestamps', () => {
  seed(); sql(apply); const first = rows(); sql(apply); assert.deepEqual(rows(), first);
});
test('missing exercise aborts the whole patch', () => rejected(`DELETE FROM speed_reading.exercises WHERE id='${patches[0].id}';`));
test('changed source timing aborts without overwriting administrator changes', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=jsonb_set(configuration_json,'{engineConfig,rules,timeLimit}','10') WHERE id='${patches[0].id}';`));
test('wrong age association aborts', () => rejected(`UPDATE speed_reading.exercises SET target_age_group_id='99999999-0000-0000-0000-000000000002' WHERE id='${patches[0].id}';`));
test('conflicting timing alias aborts', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=configuration_json||'{"timeLimitSeconds":10}'::jsonb WHERE id='${patches[0].id}';`));
test('duplicate active age/level exercise aborts', () => rejected(`UPDATE speed_reading.exercises SET type_code='SchulteTable' WHERE id='99999999-0000-0000-0000-000000000001';`));
test('missing nested grid configuration aborts atomically', () => rejected(`UPDATE speed_reading.exercises SET configuration_json=configuration_json#-'{engineConfig,gridSize}' WHERE id='${patches[0].id}';`));

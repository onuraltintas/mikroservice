import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const settings = JSON.parse(readFileSync(new URL('../../content-packs/vocabulary/v1/settings.json', import.meta.url)));
const template = readFileSync(new URL('../../content-packs/vocabulary/v1/apply.template.sql', import.meta.url), 'utf8');
const apply = readFileSync(new URL('../../content-packs/vocabulary/v1/apply.sql', import.meta.url), 'utf8');
const typeId = '30000000-0000-0000-0000-000000000001';
let container;
const docker = args => spawnSync('docker', args, { encoding: 'utf8', timeout: 60000 });
const sql = (input, success = true) => {
  const result = spawnSync('docker', ['exec', '-i', container, 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres'], { input, encoding: 'utf8', timeout: 60000 });
  if (success) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
};
before(async () => {
  const started = docker(['run', '-d', '--name', `vocabulary-test-${process.pid}-${Date.now()}`, '-e', 'POSTGRES_HOST_AUTH_METHOD=trust', 'postgres:16-alpine']);
  assert.equal(started.status, 0, started.stderr || started.error?.message);
  container = started.stdout.trim(); assert.match(container, /^[0-9a-f]{64}$/);
  for (let i = 0; i < 150; i++) {
    if (docker(['exec', container, 'pg_isready', '-U', 'postgres']).status === 0) return;
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error('Disposable PostgreSQL did not become ready');
});
after(() => { if (container) assert.equal(docker(['rm', '-fv', container]).status, 0); });
function seed() {
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,engine_type text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.exercise_types VALUES('${typeId}','Vocabulary','vocabulary_builder',true,false);
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,difficulty_level int,
      configuration_json jsonb,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,version int DEFAULT 1,
      updated_at timestamptz,updated_by text,title text,description text,type_code text,creator_id uuid,created_at timestamptz,created_by text);
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json)
      SELECT (s->>'id')::uuid,'${typeId}',(s->>'ageGroupId')::uuid,(s->>'level')::int,s->'previousConfiguration'
      FROM jsonb_array_elements($settings$${JSON.stringify(settings)}$settings$::jsonb) s WHERE s->'previousConfiguration'<>'null'::jsonb;
    CREATE TABLE speed_reading.vocabulary_items(id int,word text,definition text,difficulty_level int,target_age_group_id uuid,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.vocabulary_items SELECT level*100+n,'Kelime'||n,'Anlam'||n,level,NULL,false FROM generate_series(1,5) level CROSS JOIN generate_series(1,50) n;
    CREATE TABLE speed_reading.session_snapshots(id int,configuration_json jsonb);
    INSERT INTO speed_reading.session_snapshots VALUES(1,$snapshot$${JSON.stringify(settings[0].previousConfiguration)}$snapshot$::jsonb);`);
}
const rows = () => JSON.parse(sql('SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e;').stdout);
function rejected(mutation) {
  seed(); sql(mutation); const before = rows(); const failed = sql(apply, false);
  assert.equal(failed.status, 3, failed.stderr); assert.match(failed.stderr, /Vocabulary/); assert.deepEqual(rows(), before);
}
test('rendered SQL matches settings and template', () => assert.equal(apply, template.replace('{{SETTINGS}}', JSON.stringify(settings, null, 2))));
test('updates 28 configurations, creates child L3 quiz, preserves snapshots and is idempotent', () => {
  seed(); const snapshot = sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout;
  sql(apply); const first = rows(); assert.equal(first.length, 29);
  for (const patch of settings) {
    const row = first.find(row => row.id === patch.id);
    assert.deepEqual(row.configuration_json, patch.configuration); assert.equal(row.version, patch.previousConfiguration ? 2 : 1);
  }
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout, snapshot);
  sql(apply); assert.deepEqual(rows(), first);
});
test('missing exercise rolls back', () => rejected(`DELETE FROM speed_reading.exercises WHERE id='${settings[0].id}'`));
test('configuration drift rolls back', () => rejected(`UPDATE speed_reading.exercises SET configuration_json='{}' WHERE id='${settings[0].id}'`));
test('inactive exercise rolls back', () => rejected(`UPDATE speed_reading.exercises SET is_active=false WHERE id='${settings[0].id}'`));
test('wrong age rolls back', () => rejected(`UPDATE speed_reading.exercises SET target_age_group_id=NULL WHERE id='${settings[0].id}'`));
test('wrong engine rolls back', () => rejected(`UPDATE speed_reading.exercise_types SET engine_type='focus'`));
test('insufficient word pool rolls back', () => rejected('DELETE FROM speed_reading.vocabulary_items WHERE difficulty_level=5 AND id%100>10'));
test('ambiguous definitions roll back', () => rejected("UPDATE speed_reading.vocabulary_items SET definition='Aynı' WHERE difficulty_level=1"));
test('intersecting duplicate pairs cannot pass independent distinct-word/definition counts', () => rejected(`
  DELETE FROM speed_reading.vocabulary_items WHERE difficulty_level=1;
  INSERT INTO speed_reading.vocabulary_items SELECT n,'Kelime'||n,'Anlam'||n,1,NULL,false FROM generate_series(1,6) n;
  INSERT INTO speed_reading.vocabulary_items VALUES(101,'A','X',1,NULL,false),(102,'A','Y',1,NULL,false),(103,'B','X',1,NULL,false);`));
test('new child exercise ID collision rolls back', () => rejected(`INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json)
  VALUES('${settings.find(row => row.previousConfiguration === null).id}','${typeId}',NULL,3,'{}')`));
for (const [name, mutate] of [
  ['wrong count', config => { config.engineConfig.vocabulary.count = 1; }],
  ['timed normal quiz', config => { config.engineConfig.timeLimitPerWord = 5; }],
  ['disabled full pool', config => { delete config.engineConfig.vocabulary.requireFullPool; }],
  ['disabled learning test', config => { config.engineConfig.learnThenQuiz = false; }],
  ['different engine', config => { config.engineType = 'focus'; }],
  ['wrong difficulty', config => { config.engineConfig.vocabulary.difficultyLevel = 5; }]
]) test(`rejects payload ${name}`, () => {
  seed(); const before = rows(); const altered = structuredClone(settings); mutate(altered[0].configuration);
  const failed = sql(template.replace('{{SETTINGS}}', JSON.stringify(altered, null, 2)), false);
  assert.equal(failed.status, 3, failed.stderr); assert.deepEqual(rows(), before);
});

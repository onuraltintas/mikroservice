import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/exercise-type-texts/v1 on a disposable database:
//   TYPE_TEXTS_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres node --test tests/E2E/exercise-type-texts.postgres.test.mjs
const serverUrl = process.env.TYPE_TEXTS_TEST_DATABASE_URL;
const skip = !serverUrl && 'set TYPE_TEXTS_TEST_DATABASE_URL to run against a local PostgreSQL server';
const pack = new URL('../../content-packs/exercise-type-texts/v1/', import.meta.url);
const texts = JSON.parse(readFileSync(new URL('texts.json', pack), 'utf8'));
const database = `type_texts_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const run = (url, input, success = true) => {
  const result = spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'], { input, encoding: 'utf8', timeout: 120000 });
  if (success) assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result;
};
const sql = (input, success = true) => run(databaseUrl(), input, success);
let work;

before(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`);
  work = mkdtempSync(path.join(tmpdir(), 'type-texts-'));
});
after(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database};`);
  rmSync(work, { recursive: true, force: true });
});

const prepare = name => {
  writeFileSync(path.join(work, `${name}.json`), sql(readFileSync(new URL('snapshot.sql', pack), 'utf8')).stdout);
  const result = spawnSync(process.execPath, [new URL('prepare-release.mjs', pack).pathname, path.join(work, `${name}.json`), path.join(work, name)], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  return file => readFileSync(path.join(work, name, file), 'utf8');
};

test('applies, is idempotent, rolls back exactly and leaves other types alone', { skip }, () => {
  const rows = [...texts.map(item => item.name), 'Focus'].map((name, index) =>
    `('${`00000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`}','${name}','Göz kaslarını güçlendirir.')`).join(',');
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,description text,is_active boolean DEFAULT true,
      is_deleted boolean DEFAULT false,version int DEFAULT 2,updated_at timestamptz DEFAULT '2026-10-01T00:00:00Z',updated_by text DEFAULT 'seed');
    INSERT INTO speed_reading.exercise_types(id,name,description) VALUES ${rows};`);
  const state = () => sql(`SELECT string_agg(to_jsonb(t)::text,'|' ORDER BY name) FROM speed_reading.exercise_types t;`).stdout;
  const before = state();
  const release = prepare('release');

  sql(release('apply.sql'));
  for (const { name, description } of texts)
    assert.equal(sql(`SELECT description FROM speed_reading.exercise_types WHERE name='${name}';`).stdout.trim(), description);
  assert.equal(sql(`SELECT description FROM speed_reading.exercise_types WHERE name='Focus';`).stdout.trim(), 'Göz kaslarını güçlendirir.');
  const afterFirst = state();
  sql(release('apply.sql'));
  assert.equal(state(), afterFirst);
  sql(release('rollback.sql'));
  assert.equal(state(), before);

  // An editor change after the snapshot stops the release.
  sql(`UPDATE speed_reading.exercise_types SET description='Editör metni.' WHERE name='Chunking';`);
  const failed = sql(release('apply.sql'), false);
  assert.notEqual(failed.status, 0);
  assert.match(failed.stderr, /fresh snapshot/);
});

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/gamification-audit/v1/audit.sql (H19, read-only) on a disposable database:
//   GAMIFICATION_AUDIT_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres node --test tests/E2E/gamification-audit.postgres.test.mjs
const serverUrl = process.env.GAMIFICATION_AUDIT_TEST_DATABASE_URL;
const skip = !serverUrl && 'set GAMIFICATION_AUDIT_TEST_DATABASE_URL to run against a local PostgreSQL server';
const audit = readFileSync(new URL('../../content-packs/gamification-audit/v1/audit.sql', import.meta.url), 'utf8');
const database = `gamification_audit_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const run = (url, input) => {
  const result = spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'], { input, encoding: 'utf8', timeout: 120000 });
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result.stdout;
};

before(() => { if (!skip) run(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`); });
after(() => { if (!skip) run(serverUrl, `DROP DATABASE IF EXISTS ${database};`); });

test('lists badges nobody or only some age groups can earn, and nothing else', { skip }, () => {
  const child = '10000000-0000-0000-0000-000000000001', adult = '10000000-0000-0000-0000-000000000004';
  run(databaseUrl(), `CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.achievements(id uuid PRIMARY KEY,"Name" text,"CriteriaType" text,"CriteriaValue" text,
      "IsRepeatable" boolean DEFAULT false,"IsActive" boolean DEFAULT true,"IsDeleted" boolean DEFAULT false);
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,
      is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,is_assessment_only boolean DEFAULT false);
    CREATE TABLE speed_reading.program_templates(id uuid PRIMARY KEY,"TargetAgeGroupConfigurationId" uuid,
      "IsActive" boolean DEFAULT true,"IsDeleted" boolean DEFAULT false,"IsAssessment" boolean DEFAULT false);
    INSERT INTO speed_reading.program_templates(id,"TargetAgeGroupConfigurationId") VALUES
      ('00000000-0000-0000-0000-0000000000a1','${child}'),('00000000-0000-0000-0000-0000000000a2','${adult}');
    INSERT INTO speed_reading.exercise_types(id,name) VALUES
      ('00000000-0000-0000-0000-0000000000b1','Vocabulary'),('00000000-0000-0000-0000-0000000000b2','RSVP'),('00000000-0000-0000-0000-0000000000b3','Chunking');
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id) VALUES
      ('00000000-0000-0000-0000-0000000000c1','00000000-0000-0000-0000-0000000000b1','${child}'),
      ('00000000-0000-0000-0000-0000000000c2','00000000-0000-0000-0000-0000000000b2',NULL),
      ('00000000-0000-0000-0000-0000000000c3','00000000-0000-0000-0000-0000000000b3',NULL);
    INSERT INTO speed_reading.achievements(id,"Name","CriteriaType","CriteriaValue","IsRepeatable") VALUES
      ('00000000-0000-0000-0000-000000000001','Seri','streak','{"days":3}',false),
      ('00000000-0000-0000-0000-000000000002','Kelime','vocabulary_learned','{"count":10}',false),
      ('00000000-0000-0000-0000-000000000003','RSVP Hızı','rsvp_wpm','{"wpm":300}',false),
      ('00000000-0000-0000-0000-000000000004','İlk Gruplama','exercise_type_first','{"type":"Chunking"}',false),
      ('00000000-0000-0000-0000-000000000005','Bozuk','streak','{"count":5}',false),
      ('00000000-0000-0000-0000-000000000006','Geçersiz','streak','{bad',false),
      ('00000000-0000-0000-0000-000000000007','İlk Takistoskop','exercise_type_first','{"type":"Tachistoscope"}',false),
      ('00000000-0000-0000-0000-000000000008','Tekrar','streak','{"days":2}',true);`);

  const rows = run(databaseUrl(), audit).trim().split('\n');
  const problem = name => rows.filter(row => row.split('|')[1] === name).map(row => row.split('|')[4]);

  assert.deepEqual(problem('Seri'), []);
  assert.deepEqual(problem('RSVP Hızı'), []);
  assert.deepEqual(problem('Kelime'), [`Şu yaş grubunda kazanılamaz (öğrenciye gösterilmez): ${adult}`]);
  assert.ok(problem('İlk Gruplama')[0].startsWith('Egzersiz türü doğrulanamıyor'));
  assert.deepEqual(problem('Bozuk'), ['Kriterde gerekli alan eksik']);
  assert.deepEqual(problem('Geçersiz'), ['Kriter JSON nesnesi değil']);
  assert.deepEqual(problem('İlk Takistoskop'), ['Hiçbir yaş grubunda etkin egzersizi yok; hiç kazanılamaz']);
  assert.deepEqual(problem('Tekrar'), ['Tekrarlanabilir rozet desteklenmiyor']);
  assert.equal(rows.at(-1), 'Etkin rozet: 8');
});

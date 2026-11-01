import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

// Rehearses content-packs/parent-weekly-summary-mute/v1 (V6a, 01.11.2026) on a disposable database:
//   PARENT_SUMMARY_MUTE_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres node --test tests/E2E/parent-weekly-summary-mute.postgres.test.mjs
// check.sql (Identity, read-only) finds the parents who turned the legacy weekly summary off, before or after the migration;
// apply-notification.sql (Notification) turns the ParentSummary e-mail channel off for exactly those parents.
const serverUrl = process.env.PARENT_SUMMARY_MUTE_TEST_DATABASE_URL;
const skip = !serverUrl && 'set PARENT_SUMMARY_MUTE_TEST_DATABASE_URL to run against a local PostgreSQL server';
const pack = name => readFileSync(new URL(`../../content-packs/parent-weekly-summary-mute/v1/${name}`, import.meta.url), 'utf8');
const database = `parent_summary_mute_test_${process.pid}`;
const databaseUrl = () => { const url = new URL(serverUrl); url.pathname = `/${database}`; return url.toString(); };
const psql = (url, input, variables = {}) => spawnSync('psql', [url, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1',
  ...Object.entries(variables).flatMap(([key, value]) => ['-v', `${key}=${value}`])], { input, encoding: 'utf8', timeout: 120000 });
const run = (url, input, variables) => {
  const result = psql(url, input, variables);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  return result.stdout.trim();
};

const muted = ['00000000-0000-0000-0000-0000000000a1', '00000000-0000-0000-0000-0000000000a2'];
const listening = '00000000-0000-0000-0000-0000000000b1';
const untouched = '00000000-0000-0000-0000-0000000000c1';

before(() => {
  if (skip) return;
  run(serverUrl, `DROP DATABASE IF EXISTS ${database}; CREATE DATABASE ${database};`);
  run(databaseUrl(), `CREATE SCHEMA identity;
    CREATE TABLE identity.parent_profiles("Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),"UserId" uuid NOT NULL,"NotificationPreferences" jsonb NOT NULL);
    INSERT INTO identity.parent_profiles("UserId","NotificationPreferences") VALUES
      ('${muted[1]}','{"dailyReport": true, "weeklyProgress": false}'),
      ('${muted[0]}','{"weeklyProgress": false}'),
      ('${listening}','{"dailyReport": false, "weeklyProgress": true, "examResults": true, "lowActivityAlert": true}'),
      ('${untouched}','{}');
    CREATE TABLE "NotificationPreferences"("UserId" uuid NOT NULL,"Category" varchar(40) NOT NULL,"InApp" boolean NOT NULL,"Email" boolean NOT NULL,
      "UpdatedAt" timestamptz NOT NULL,"Sms" boolean NOT NULL DEFAULT false,"WhatsApp" boolean NOT NULL DEFAULT false,PRIMARY KEY("UserId","Category"));
    INSERT INTO "NotificationPreferences" VALUES
      ('${muted[0]}','ParentSummary',false,true,'2026-10-01',true,false),
      ('${muted[0]}','Messages',true,true,'2026-10-01',false,false),
      ('${listening}','ParentSummary',true,true,'2026-10-01',false,false);`);
});
after(() => { if (!skip) run(serverUrl, `DROP DATABASE IF EXISTS ${database};`); });

test('check.sql counts the muted parents before the migration and from its backup afterwards', { skip }, () => {
  assert.equal(run(databaseUrl(), pack('check.sql')), `2|${muted.join(',')}`);
  run(databaseUrl(), `CREATE TABLE identity.legacy_parent_notification_preferences_20261101 AS
      SELECT "UserId" parent_user_id,"NotificationPreferences" notification_preferences FROM identity.parent_profiles;
    ALTER TABLE identity.parent_profiles DROP COLUMN "NotificationPreferences";`);
  assert.equal(run(databaseUrl(), pack('check.sql')), `2|${muted.join(',')}`);
  run(databaseUrl(), 'DROP TABLE identity.legacy_parent_notification_preferences_20261101;');
  const missing = psql(databaseUrl(), pack('check.sql'));
  assert.notEqual(missing.status, 0, 'neither the column nor its backup: the operator must not read this as "0"');
});

test('apply-notification.sql turns only the ParentSummary e-mail off, only for the listed parents, and refuses a wrong count', { skip }, () => {
  const before = run(databaseUrl(), 'SELECT string_agg("UserId"||\':\'||"Category"||\':\'||"Email", \',\' ORDER BY 1) FROM "NotificationPreferences";');
  const wrong = psql(databaseUrl(), pack('apply-notification.sql'), { parent_ids: muted.join(','), expected_count: '3' });
  assert.notEqual(wrong.status, 0);
  assert.match(wrong.stderr, /expected 3/);
  assert.equal(run(databaseUrl(), 'SELECT string_agg("UserId"||\':\'||"Category"||\':\'||"Email", \',\' ORDER BY 1) FROM "NotificationPreferences";'), before);

  const bad = psql(databaseUrl(), pack('apply-notification.sql'), { parent_ids: 'not-a-uuid', expected_count: '1' });
  assert.notEqual(bad.status, 0);

  const output = run(databaseUrl(), pack('apply-notification.sql'), { parent_ids: muted.join(','), expected_count: '2' });
  assert.match(output, /parents: 2 \| e-mail on before: 1 \| already off: 0 \| no row before: 1/);
  assert.match(output, /e-mail off after: 2/);
  const rows = run(databaseUrl(), `SELECT string_agg("UserId"||':'||"Category"||':'||"InApp"||':'||"Email"||':'||"Sms", ',' ORDER BY "UserId","Category")
    FROM "NotificationPreferences";`).split(',');
  assert.deepEqual(rows, [
    `${muted[0]}:Messages:true:true:false`,
    `${muted[0]}:ParentSummary:false:false:true`,
    `${muted[1]}:ParentSummary:true:false:false`,
    `${listening}:ParentSummary:true:true:false`,
  ]);

  const again = run(databaseUrl(), pack('apply-notification.sql'), { parent_ids: muted.join(','), expected_count: '2' });
  assert.match(again, /e-mail on before: 0 \| already off: 2 \| no row before: 0/, 'running it twice changes nothing');
});

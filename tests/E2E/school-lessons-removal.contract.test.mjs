import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// KM8 (01.11.2026): the coaching catalog keeps only exam lessons; content-packs/school-lessons-removal/v1 removes the
// 141 exam-less (school) lessons. Database rehearsal: tests/E2E/school-lessons-removal.postgres.test.mjs.
//   node --test tests/E2E/school-lessons-removal.contract.test.mjs
const packDir = fileURLToPath(new URL('../../content-packs/school-lessons-removal/v1/', import.meta.url));
const read = name => readFileSync(packDir + name, 'utf8');
const lessons = () => read('lessons.csv').trim().split('\n');

test('the lesson list holds the 141 exam-less lessons, ids and names only', () => {
  const [header, ...rows] = lessons();
  assert.equal(header, 'Id,Name');
  assert.equal(rows.length, 141);
  assert.equal(new Set(rows.map(x => x.split(',')[0])).size, 141);
  // The exam-less duplicates of exam lessons are on it too.
  for (const name of ['Mantık', 'Psikoloji', 'Sosyoloji', 'YDT İngilizce', 'YDT Arapça'])
    assert.ok(rows.some(x => x.endsWith(`,${name}`)), name);
});

test('the SQL files carry the current list', () => {
  const result = spawnSync(process.execPath, [packDir + 'build.mjs', '--check'], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr || result.stdout);
  for (const file of ['check.sql', 'apply.sql', 'verify.sql', 'restore.sql'])
    assert.equal(read(file).match(/^ {4}\('[0-9a-f-]{36}'::uuid, '/gm)?.length, 141, file);
});

test('check.sql and verify.sql are read-only; apply.sql and restore.sql are one guarded transaction', () => {
  for (const file of ['check.sql', 'verify.sql']) {
    const sql = read(file);
    assert.match(sql, /^BEGIN READ ONLY;/m, file);
    assert.doesNotMatch(sql, /\b(INSERT INTO|DELETE FROM|UPDATE coaching|CREATE TABLE|DROP TABLE)\b/i, file);
  }
  for (const file of ['apply.sql', 'restore.sql']) {
    const sql = read(file);
    assert.equal(sql.match(/^BEGIN;$/gm)?.length, 1, file);
    assert.equal(sql.match(/^COMMIT;$/gm)?.length, 1, file);
    assert.match(sql, /SET LOCAL lock_timeout/, file);
    // The same advisory lock the admin catalog writes take (CoachingCatalogAdminToolsService).
    assert.match(sql, /pg_advisory_xact_lock\(4188, 100\)/, file);
    assert.match(sql, /Nothing changed/, file);
  }
  // Plain SQL only (no psql meta-commands or variables), so the C# Postgres test can run the same files.
  for (const file of ['check.sql', 'apply.sql', 'verify.sql', 'restore.sql'])
    assert.doesNotMatch(read(file), /^\s*\\|(?<!:):(?!:)'?[a-z_][a-z0-9_]*/m, file);
});

test('apply.sql backs up before it deletes and treats every student reference as a reason to deactivate', () => {
  const sql = read('apply.sql');
  for (const table of ['lessons', 'units', 'topics']) {
    const backup = sql.indexOf(`INSERT INTO coaching.removed_school_catalog_20261101_${table}`);
    const remove = sql.indexOf(`DELETE FROM coaching.study_catalog_${table}`);
    assert.ok(backup > 0 && remove > backup, table);
  }
  for (const reference of ['student_course_preferences', 'student_topic_states', 'study_plan_tasks', '"WeeklyLessonId"',
    'study_weekly_drafts', 'lesson_answers', 'topic_analysis'])
    for (const file of ['check.sql', 'apply.sql']) assert.ok(read(file).includes(reference), `${file}: ${reference}`);
  assert.match(sql, /coaching\."AdminAuditRecords"/);
  assert.match(sql, /'CatalogPermanentDelete'/);
  assert.match(sql, /'CatalogStatus'/);
});

test('the README tells the operator to run check.sql and report the counts before apply', () => {
  const readme = read('README.md');
  assert.match(readme, /check\.sql/);
  assert.ok(readme.indexOf('check.sql') < readme.indexOf('apply.sql'));
  assert.match(readme, /report/i);
  assert.match(readme, /restore\.sql/);
});

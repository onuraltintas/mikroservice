import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

// KM3 minutes pack (01.11.2026, user decision "sınırı yükselt"): the per-topic limit goes from 1440 to 4320 minutes, so
// the KM3 topics stored at 1440 although their estimate is larger get the uncapped estimate. Topics are never split.
const dir = new URL('../../content-packs/coaching-catalog-km3-minutes/v1/', import.meta.url);
const km3 = new URL('../../content-packs/coaching-catalog-km3/', import.meta.url);
const read = name => readFileSync(new URL(name, dir), 'utf8');
const node = (script, ...args) => spawnSync(process.execPath, [new URL(script).pathname, ...args], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
const csv = text => { const [head, ...rows] = text.trim().split('\n'); const keys = head.split(',');
  return rows.map(r => { const cells = []; let cell = '', quoted = false;
    for (let i = 0; i < r.length; i++) { const ch = r[i];
      if (quoted) { if (ch === '"' && r[i + 1] === '"') { cell += '"'; i++; } else if (ch === '"') quoted = false; else cell += ch; }
      else if (ch === '"') quoted = true; else if (ch === ',') { cells.push(cell); cell = ''; } else cell += ch; }
    cells.push(cell); return Object.fromEntries(keys.map((k, i) => [k, cells[i]])); }); };

test('generated files are current', () => {
  const result = node(new URL('build.mjs', dir), '--check');
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const km3Check = node(new URL('build.mjs', km3), '--check');
  assert.equal(km3Check.status, 0, km3Check.stdout + km3Check.stderr);
});

test('KM3 derives topic minutes with the 4320-minute limit; a fresh install gets them directly', () => {
  const catalog = JSON.parse(readFileSync(new URL('catalog.json', km3), 'utf8'));
  assert.equal(catalog.method.maxMinutes, 4320);
  assert.equal(catalog.previousRelease.maxMinutes, 1440);
  const model = JSON.parse(node(new URL('build.mjs', km3), '--json').stdout);
  for (const lesson of model.lessons) for (const t of lesson.topics) {
    assert.equal(t.minutes, Math.min(t.rawMinutes, 4320), t.code);
    assert.equal(t.previousMinutes, Math.min(t.rawMinutes, 1440), t.code);
  }
  assert.match(readFileSync(new URL('verify.sql', km3), 'utf8'), /NOT BETWEEN 1 AND 4320/);
});

test('changes exactly the KM3 topics capped at 1440 whose uncapped estimate is larger (by id)', () => {
  const rows = csv(read('topics.csv'));
  const review = csv(readFileSync(new URL('catalog-review.csv', km3), 'utf8'));
  const capped = review.filter(x => Number(x.minutes) > 1440 || (Number(x.raw_minutes) > 1440));
  assert.equal(rows.length, 39);
  assert.deepEqual(rows.map(x => x.topic_code), capped.map(x => x.topic_code));
  for (const row of rows) {
    const r = review.find(x => x.topic_code === row.topic_code);
    assert.equal(Number(row.old_minutes), 1440, row.topic_code);
    assert.equal(Number(row.new_minutes), Math.min(Number(r.raw_minutes), 4320), row.topic_code);
    assert.ok(Number(row.new_minutes) > 1440 && Number(row.new_minutes) <= 4320, row.topic_code);
    assert.match(row.topic_id, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
    assert.ok(readFileSync(new URL('apply.sql', km3), 'utf8').includes(`('${row.topic_id}', '${row.topic_code}'`), row.topic_code);
  }
  // Topics at exactly 1440 whose estimate is 1440 stay as they are.
  for (const code of ['OABT_SOS.1.03', 'OABT_SOS.1.04', 'OABT_TAR.1.07', 'OABT_REH.1.01', 'OABT_BED.1.01'])
    assert.ok(!rows.some(x => x.topic_code === code), code);
  const analiz = rows.find(x => x.topic_code === 'OABT_IMAT.1.01'), uygulama = rows.find(x => x.topic_code === 'OABT_IMAT.1.04');
  assert.ok(Number(analiz.new_minutes) > Number(uygulama.new_minutes), 'Analiz (%36) now gets more time than Uygulamalı Matematik (%16)');
});

test('apply.sql is guarded: one transaction, lock timeout, the importer and admin locks, reviewer, drift refusal, notices, audit', () => {
  const sql = read('apply.sql');
  assert.equal(sql.match(/^BEGIN;$/gm)?.length, 1); assert.equal(sql.match(/^COMMIT;$/gm)?.length, 1);
  assert.match(sql, /SET LOCAL lock_timeout = '5s'/);
  assert.match(sql, /pg_advisory_xact_lock\(hashtext\('coaching-catalog-km3'\)\)/);
  assert.match(sql, /pg_advisory_xact_lock\(4188, 100\)/);
  assert.match(sql, /reviewer_id/);
  assert.match(sql, /Nothing changed/);
  assert.match(sql, /RAISE NOTICE 'before:/); assert.match(sql, /RAISE NOTICE 'after:/);
  assert.match(sql, /INSERT INTO coaching\."AdminAuditRecords"/);
  assert.match(sql, /'ReviewedStudy'/);
  assert.doesNotMatch(sql, /\bDELETE\b/i);
  assert.doesNotMatch(sql, /"IsPublished"\s*=\s*false/i, 'no published duration version is withdrawn');
  for (const name of ['check.sql', 'verify.sql']) {
    const readOnly = read(name);
    assert.match(readOnly, /BEGIN READ ONLY;/, name);
    assert.doesNotMatch(readOnly, /\b(INSERT|UPDATE|DELETE)\b/, name);
  }
  assert.match(read('README.md'), /4320/);
});

test('when the production KM3 release is in git history, the old values are exactly what it wrote', { skip: spawnSync('git', ['cat-file', '-e', '5056431e:content-packs/coaching-catalog-km3/apply.sql']).status !== 0 && 'release 5056431e not in this clone' }, () => {
  const old = spawnSync('git', ['show', '5056431e:content-packs/coaching-catalog-km3/apply.sql'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }).stdout;
  for (const row of csv(read('topics.csv'))) {
    const line = old.split('\n').find(x => x.startsWith(`    ('${row.topic_id}', '${row.topic_code}'`));
    assert.ok(line, row.topic_code);
    assert.match(line, /, 1440, \d, /, row.topic_code);
  }
  const sql = read('apply.sql');
  for (const line of old.split('\n').filter(x => x.includes('planlayıcı sınırı nedeniyle 1440 dk kaydedildi'))) {
    const notes = line.match(/', '((?:[^']|'')*planlayıcı sınırı nedeniyle 1440 dk kaydedildi(?:[^']|'')*)'/)[1];
    assert.ok(sql.includes(`'${notes}'`), notes.slice(0, 80));
  }
});

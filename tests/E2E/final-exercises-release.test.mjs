import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildBatch, buildInventorySql, packs } from '../../tools/releases/final-exercises-20261012/prepare.mjs';

test('three pending packages are one atomic release, not previous published packages', () => {
  assert.deepEqual(packs, ['skimming/v1', 'eye-tracking/v1', 'exam-simulation/v1']);
  const sql = buildBatch();
  assert.equal((sql.match(/^BEGIN;$/gm) ?? []).length, 1);
  assert.equal((sql.match(/^COMMIT;$/gm) ?? []).length, 1);
  for (const pack of packs) assert.ok(sql.includes(`-- Package: ${pack}`));
  assert.throws(() => buildBatch(() => 'BEGIN;\nCOMMIT;\nCOMMIT;'), /transaction boundaries/);
});

test('inventory protects history and immutable catalog fields and checks idempotence', () => {
  assert.match(buildInventorySql(), /target-exercises-immutable/);
  assert.match(buildInventorySql(), /WHEN 'reading_questions' THEN/);
  assert.doesNotMatch(buildInventorySql(true), /NOT IN/);
});

test('promotion requires checksum, rehearsal, backup and only two scoped restarts', () => {
  const script = readFileSync(new URL('../../tools/releases/final-exercises-20261012/deploy.sh', import.meta.url), 'utf8');
  for (const value of ['flock -n 9', 'test -s rehearsal-passed.txt', 'sha256sum -c payload.sha256',
    'backup speedreading-before.dump', 'diff -u rehearsal-first.txt rehearsal-second.txt',
    'up -d --no-deps --pull never speed-reading-service speed-reading-frontend']) assert.ok(script.includes(value));
  assert.doesNotMatch(script, /system prune|volume rm/);
  assert.ok(script.includes('< rollback-all.sql'));
  assert.ok(script.includes('< verify-rollback.sql'));
});

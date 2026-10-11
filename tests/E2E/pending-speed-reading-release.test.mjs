import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildBatch, buildInventorySql, packs } from '../../tools/releases/pending-speed-reading-20261011/prepare.mjs';

test('the approved five packages share exactly one atomic transaction', () => {
  const sql = buildBatch();
  assert.equal((sql.match(/^BEGIN;$/gm) ?? []).length, 1);
  assert.equal((sql.match(/^COMMIT;$/gm) ?? []).length, 1);
  assert.deepEqual(packs, ['focus/v1', 'vocabulary/v1', 'free-reading/v1', 'error-analysis/v1', 'rsvp/v2']);
  for (const pack of packs) assert.ok(sql.includes(`-- Package: ${pack}`));
});

test('malformed transaction boundaries prevent any deployment batch', () => {
  assert.throws(() => buildBatch(() => 'BEGIN;\nCOMMIT;\nCOMMIT;'), /transaction boundaries/);
});

test('inventory protects all history, programs and unrelated content, including target immutable fields', () => {
  const sql = buildInventorySql();
  assert.match(sql, /BEGIN READ ONLY/);
  assert.match(sql, /schemaname IN \('speed_reading','public'\)/);
  assert.match(sql, /target-exercises-immutable/);
  assert.match(sql, /configuration_json.*title.*updated_at.*updated_by.*version/s);
  assert.match(sql, /WHEN 'reading_questions' THEN/);
  assert.doesNotMatch(sql, /DELETE|UPDATE|DROP TABLE/);
});

test('idempotence inventory excludes no table or row', () => {
  const sql = buildInventorySql(true);
  assert.doesNotMatch(sql, /NOT IN|target-exercises-immutable/);
  assert.match(sql, /row_to_json/);
});

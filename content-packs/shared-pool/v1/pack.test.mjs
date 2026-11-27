import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { build, SHAREABLE } from './generate.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const read = name => readFileSync(join(directory, name), 'utf8');
const files = ['moves.json', 'bands.json', 'apply.sql', 'rollback.sql'];
const result = build();

test('generated files are current', () => {
  const before = files.map(read);
  execFileSync(process.execPath, [join(directory, 'generate.mjs')]);
  assert.deepEqual(files.map(read), before);
});

test('moves only plain reading texts and never an evaluation story or a second copy of a story', () => {
  assert.equal(result.moves.length, 246);
  assert.equal(new Set(result.moves.map(move => move.id)).size, 246);
  for (const move of result.moves) assert.ok(SHAREABLE.includes(move.source), move.source);
  assert.ok(!result.moves.some(move => ['ErrorAnalysis', 'Scanning', 'Skimming', 'SpeedReading', 'Comprehension:evaluation'].includes(move.source)));
  const skippedIds = new Set(result.skipped.map(item => item.id));
  assert.ok(result.moves.every(move => !skippedIds.has(move.id)));
  const counts = Object.fromEntries(SHAREABLE.map(source => [source, result.moves.filter(move => move.source === source).length]));
  assert.deepEqual(counts, { FreeReading: 79, Chunking: 57, RegressionReduction: 34, SubvocalizationReduction: 35, RSVP: 22,
    TextFading: 3, 'Comprehension:learning': 16 });
});

test('Kelime Gruplama ranges only widen and cover every pool text of their age and level', () => {
  assert.equal(result.bands.length, 20);
  for (const band of result.bands) {
    assert.ok(band.to[0] <= band.from[0] && band.to[1] >= band.from[1], `${band.age} ${band.level}`);
    assert.ok(band.to[0] >= 100 && band.to[1] <= 1050, `${band.age} ${band.level}`);
  }
});

test('SQL is guarded, idempotent and reversible', () => {
  const apply = read('apply.sql'), rollback = read('rollback.sql');
  assert.match(apply, /md5\(t\.content\) = m\.content_md5/);
  assert.match(apply, /IF ready <> 246 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /IF banded <> 20 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /WHERE t\.id = m\.id AND t\.exercise_id = m\.exercise_id/);
  assert.doesNotMatch(apply + rollback, /DELETE FROM|DROP TABLE speed_reading|TRUNCATE/);
  assert.match(rollback, /t\.exercise_id IS NULL AND t\.updated_by = 'shared-pool-v1'/);
  for (const move of result.moves) {
    assert.ok(apply.includes(`'${move.id}'::uuid, '${move.exerciseId}'::uuid`));
    assert.ok(rollback.includes(`'${move.id}'::uuid, '${move.exerciseId}'::uuid`));
  }
});

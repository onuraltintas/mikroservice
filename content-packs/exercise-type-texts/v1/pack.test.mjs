import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { overstatedClaims } from '../../../tests/E2E/support/speed-reading-claims.mjs';

// Decision 30.10.2026: exercise descriptions say what the exercise practises, without unproven promises such as
// "strengthens eye muscles" or "widens the visual field".
const texts = JSON.parse(fs.readFileSync(new URL('./texts.json', import.meta.url), 'utf8'));
const programs = JSON.parse(fs.readFileSync(new URL('../../programs/v6/programs.json', import.meta.url), 'utf8'));

test('covers every exercise type the programs use, once', () => {
  const used = new Set(programs.flatMap(program => Object.entries(program.pattern)
    .filter(([key]) => key.startsWith('week')).flatMap(([, week]) => Object.values(week).flat().map(slot => slot.type))));
  assert.deepEqual(new Set(texts.map(item => item.name)), used);
  assert.equal(new Set(texts.map(item => item.name)).size, texts.length);
});

test('descriptions are short, complete Turkish sentences without overstated claims', () => {
  for (const { name, description } of texts) {
    assert.ok(description.length >= 40 && description.length <= 240, name);
    assert.match(description, /\.$/, name);
    assert.deepEqual(overstatedClaims(description), [], name);
  }
});

test('prepare-release writes guarded apply and exact rollback from a snapshot', () => {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), 'type-texts-'));
  const snapshot = texts.map((item, index) => ({ id: `00000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`, name: item.name,
    description: 'Göz kaslarını güçlendirir.', version: 3, updatedAt: null, updatedBy: null }));
  fs.writeFileSync(path.join(work, 'snapshot.json'), JSON.stringify(snapshot));
  const result = spawnSync(process.execPath, [new URL('./prepare-release.mjs', import.meta.url).pathname, path.join(work, 'snapshot.json'), path.join(work, 'release')], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const apply = fs.readFileSync(path.join(work, 'release', 'apply.sql'), 'utf8');
  const rollback = fs.readFileSync(path.join(work, 'release', 'rollback.sql'), 'utf8');
  assert.match(apply, /RAISE EXCEPTION/);
  assert.match(apply, /exercise-type-texts-v1/);
  assert.match(rollback, /previousDescription/);
  const missing = spawnSync(process.execPath, [new URL('./prepare-release.mjs', import.meta.url).pathname, path.join(work, 'missing.json'), path.join(work, 'r2')], { encoding: 'utf8' });
  assert.notEqual(missing.status, 0);
  fs.writeFileSync(path.join(work, 'short.json'), JSON.stringify(snapshot.slice(1)));
  const short = spawnSync(process.execPath, [new URL('./prepare-release.mjs', import.meta.url).pathname, path.join(work, 'short.json'), path.join(work, 'r3')], { encoding: 'utf8' });
  assert.notEqual(short.status, 0);
  fs.rmSync(work, { recursive: true, force: true });
});

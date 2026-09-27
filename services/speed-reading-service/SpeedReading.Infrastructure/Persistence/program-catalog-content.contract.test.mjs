import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const migration = readFileSync(new URL('./Migrations/20260923103000_CompleteAssignedProgramCatalog.cs', import.meta.url), 'utf8');

test('program catalog adds distinct playable variants and age-matched comprehension texts', () => {
  const variants = [...migration.matchAll(/^\s*\('([^']+)', '([^']+)', '[^']+', '[^']+', '(\{.*\})'::jsonb\),?$/gm)]
    .map(([, key, type, config]) => ({ key, type, config: JSON.parse(config) }));
  assert.equal(variants.length, 18);
  assert.equal(new Set(variants.map(({ key }) => key)).size, variants.length);
  for (const type of ['VisualExpansion', 'SchulteTable', 'EyeTracking', 'FreeReading']) {
    assert.ok(variants.some((variant) => variant.type === type), type);
  }
  for (const variant of variants) {
    assert.equal(variant.config.difficultyLevel, 1, variant.key);
    assert.equal(typeof variant.config.engineType, 'string', variant.key);
  }
  assert.match(migration, /reading_questions/);
  assert.match(migration, /targetReadingTextId/);
  assert.match(migration, /'10000000-0000-0000-0000-000000000001'::uuid, 2, 1/);
  assert.match(migration, /'10000000-0000-0000-0000-000000000002'::uuid, 2, 4/);
  assert.match(migration, /'10000000-0000-0000-0000-000000000004'::uuid, 3, 4/);
  assert.match(migration, /ON CONFLICT \(id\) DO NOTHING/);
});

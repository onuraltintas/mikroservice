import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const load = () => JSON.parse(readFileSync(new URL('../../content-packs/schulte-settings/v2/settings.json', import.meta.url), 'utf8'));
const expected = {
  Child: [[3,45],[3,30],[4,60],[4,45],[5,90]],
  Teen: [[4,60],[4,45],[5,90],[5,60],[6,120]],
  YoungAdult: [[4,60],[5,90],[5,60],[6,120],[7,150]],
  Adult: [[4,60],[5,90],[5,60],[6,120],[7,150]]
};
const folders = { Child: 'child', Teen: 'teen', YoungAdult: 'young-adult', Adult: 'adult' };

test('all twenty age/level settings match the approved progression and explicit time caps', () => {
  const rows = load();
  assert.equal(rows.length, 20);
  assert.equal(new Set(rows.map(row => row.id)).size, 20);
  for (const [age, pairs] of Object.entries(expected)) {
    assert.deepEqual(rows.filter(row => row.age === age).map(row => [row.gridSize, row.timeLimit]), pairs);
    assert.deepEqual(rows.filter(row => row.age === age).map(row => row.level), [1,2,3,4,5]);
  }
});

test('patches retain the exact exercise identities and expected original settings', () => {
  for (const row of load()) {
    const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${folders[row.age]}-exercises/v1/catalog.json`, import.meta.url), 'utf8'));
    const old = catalog.find(item => item.id === row.id);
    assert.equal(old.type, 'SchulteTable');
    assert.equal(old.targetAgeGroupId, row.ageGroupId);
    assert.equal(old.difficultyLevel, row.level);
    assert.equal(old.configuration.engineConfig.gridSize, row.oldGridSize);
    assert.equal(old.configuration.engineConfig.rules.timeLimit, row.oldTimeLimit);
  }
});

test('SQL embeds exactly the checked patch list', () => {
  const sql = readFileSync(new URL('../../content-packs/schulte-settings/v2/apply.sql', import.meta.url), 'utf8');
  const data = sql.match(/\$settings\$([\s\S]*?)\$settings\$/);
  assert.ok(data);
  assert.deepEqual(JSON.parse(data[1]), load());
});

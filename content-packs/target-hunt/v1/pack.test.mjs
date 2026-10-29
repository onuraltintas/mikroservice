import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

// Decision 29.10.2026: "Hedef Avı" replaces N-back in programs, so every age group needs levels 1–5.
const settings = JSON.parse(fs.readFileSync(new URL('./settings.json', import.meta.url), 'utf8'));
const template = fs.readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
const apply = fs.readFileSync(new URL('./apply.sql', import.meta.url), 'utf8');
const ages = ['10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002',
  '10000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000003'];

test('twenty exercises: four age groups with levels one to five and stable unique ids', () => {
  assert.equal(settings.exercises.length, 20);
  assert.equal(new Set(settings.exercises.map(row => row.id)).size, 20);
  for (const age of ages)
    assert.deepEqual(settings.exercises.filter(row => row.ageGroupId === age).map(row => row.level).sort(), [1, 2, 3, 4, 5]);
  assert.match(settings.type.id, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
  assert.equal(settings.type.name, 'TargetHunt');
  assert.equal(settings.type.engineType, 'target_hunt');
});

test('every configuration is playable and matches the server rules', () => {
  for (const row of settings.exercises) {
    const config = row.configuration.engineConfig;
    assert.equal(row.configuration.engineType, 'target_hunt');
    assert.equal(row.configuration.difficultyLevel, row.level);
    assert.ok(config.target && !/\s/.test(config.target), row.id);
    assert.ok(config.distractors.length >= 1 && !config.distractors.includes(config.target), row.id);
    assert.equal(new Set(config.distractors).size, config.distractors.length, row.id);
    assert.ok(config.rows >= 3 && config.rows <= 12 && config.columns >= 3 && config.columns <= 12, row.id);
    assert.ok(config.targetCount >= 1 && config.targetCount * 2 <= config.rows * config.columns, row.id);
    assert.ok(config.timeLimit >= 20 && config.timeLimit <= 600, row.id);
    assert.ok(row.title.startsWith('Hedef Avı'), row.title);
  }
});

test('within an age group a higher level never has fewer items or more time per item', () => {
  for (const age of ages) {
    const rows = settings.exercises.filter(row => row.ageGroupId === age).sort((a, b) => a.level - b.level);
    for (let index = 1; index < rows.length; index++) {
      const before = rows[index - 1].configuration.engineConfig, after = rows[index].configuration.engineConfig;
      assert.ok(after.rows * after.columns >= before.rows * before.columns, `${age} S${rows[index].level}`);
      assert.ok(after.timeLimit / (after.rows * after.columns) <= before.timeLimit / (before.rows * before.columns) + 1e-9,
        `${age} S${rows[index].level}`);
    }
  }
});

test('the rendered SQL is the template with the approved settings', () => {
  assert.equal(apply, template.replace('{{SETTINGS}}', JSON.stringify(settings, null, 2)));
});

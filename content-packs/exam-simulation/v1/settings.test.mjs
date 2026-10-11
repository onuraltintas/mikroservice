import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';

test('three bounded level-five exam profiles have eight questions and separate reading time', async () => {
  assert.ok(existsSync(new URL('generate.mjs', import.meta.url)), 'Exam package generator is required');
  const { buildExamPack } = await import('./generate.mjs');
  const settings = buildExamPack();
  assert.equal(settings.length, 3);
  assert.equal(new Set(settings.map(row => row.ageGroupId)).size, 3);
  for (const row of settings) {
    assert.equal(row.level, 5);
    assert.equal(row.configuration.engineType, 'exam_simulation');
    const { content, timing } = row.configuration.engineConfig;
    assert.equal(content.questionCount, 8);
    assert.ok(content.minWordCount > 0 && content.maxWordCount > content.minWordCount);
    assert.ok(timing.minReadingTimeMs < timing.maxReadingTimeMs);
    assert.ok(timing.questionTimeSeconds >= 48);
  }
});

test('generated apply and rollback embed the same settings without modifying history', () => {
  const settingsPath = new URL('settings.json', import.meta.url);
  assert.ok(existsSync(settingsPath), 'Generated exam settings are required');
  const settings = JSON.parse(readFileSync(settingsPath, 'utf8'));
  for (const name of ['apply', 'rollback']) {
    const sql = readFileSync(new URL(`${name}.sql`, import.meta.url), 'utf8');
    assert.deepEqual(JSON.parse(sql.split('$settings$')[1]), settings);
    assert.match(sql, /BEGIN;/); assert.match(sql, /COMMIT;/);
    assert.doesNotMatch(sql, /DELETE FROM|TRUNCATE|UPDATE speed_reading\.(exercise_sessions|reading_texts)/i);
  }
});

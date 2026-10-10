import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const root = new URL('../../content-packs/scanning/v1/', import.meta.url);
test('scanning pack provides all age/level settings and three real passages per exercise', () => {
  const settings = JSON.parse(readFileSync(new URL('settings.json', root), 'utf8'));
  const texts = JSON.parse(readFileSync(new URL('content.json', root), 'utf8'));
  const sql = readFileSync(new URL('apply.sql', root), 'utf8');
  assert.deepEqual(JSON.parse(sql.split('$settings$')[1]), settings);
  assert.deepEqual(JSON.parse(sql.split('$content$')[1]), texts);
  assert.equal(settings.length, 20);
  assert.equal(texts.length, 60);
  assert.equal(new Set(texts.map(text => text.id)).size, 60);
  assert.equal(new Set(texts.map(text => text.content)).size, 60);
  for (const age of ['child', 'teen', 'young-adult', 'adult']) {
    const rows = settings.filter(item => item.age === age).sort((a, b) => a.level - b.level);
    assert.deepEqual(rows.map(item => item.level), [1, 2, 3, 4, 5]);
    let previous = 0;
    for (const row of rows) {
      const config = row.configuration.engineConfig;
      assert.ok(config.content.minWordCount > previous);
      previous = config.content.minWordCount;
      assert.ok(config.timeLimit >= 30 && config.timeLimit <= 180);
      const passages = texts.filter(text => text.exerciseId === row.id);
      assert.equal(passages.length, 3);
      for (const text of passages) {
        const words = text.content.trim().split(/\s+/);
        assert.equal(words.length, text.wordCount);
        assert.ok(words.length >= config.content.minWordCount && words.length <= config.content.maxWordCount);
        assert.equal(text.ageGroupId, row.ageGroupId);
        assert.equal(text.level, row.level);
        if (row.level >= 3) {
          const tasks = config.targets.tasksByTextId[text.id];
          assert.equal(tasks.length, config.targetCount);
          const normalize = word => word.normalize('NFC').replace(/^[\p{P}\p{S}]+|[\p{P}\p{S}]+$/gu, '').toLocaleLowerCase('tr-TR');
          assert.equal(new Set(tasks.map(task => normalize(task.answer))).size, tasks.length);
          for (const task of tasks) {
            assert.ok(task.prompt && task.answer);
            assert.ok(words.map(normalize).includes(normalize(task.answer)));
          }
          assert.ok(tasks.some(task => /\d{2}\.\d{2}\.\d{4}/.test(task.answer)));
          assert.ok(tasks.some(task => /^\d+$/.test(task.answer)));
          assert.ok(tasks.some(task => /^\p{Lu}/u.test(task.answer)));
        }
      }
    }
  }
});

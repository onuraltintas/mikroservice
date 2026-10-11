import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const counts = { child: [8,10,12,14,16], teen: [10,12,14,16,18], 'young-adult': [12,14,16,18,20], adult: [12,15,18,20,24] };
for (const [age, expected] of Object.entries(counts)) {
  test(`${age}: approved counts, unlimited time and learn-then-test share one pool`, () => {
    const rows = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)))
      .filter(row => row.type === 'Vocabulary');
    for (const row of rows) {
      const config = row.configuration.engineConfig;
      assert.equal(config.vocabulary.count, expected[row.difficultyLevel - 1]);
      assert.equal(config.vocabulary.difficultyLevel, row.difficultyLevel);
      assert.equal(config.vocabulary.requireFullPool, true);
      assert.equal(config.timeLimitPerWord, 0);
      if (config.mode === 'learning') assert.equal(config.learnThenQuiz, true);
    }
    for (let level = 1; level <= 5; level++) assert.ok(rows.some(row => row.difficultyLevel === level && row.configuration.engineConfig.mode === 'quiz'));
  });
}

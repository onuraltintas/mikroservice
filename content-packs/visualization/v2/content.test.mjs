import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const ages = ['child', 'teen', 'young-adult', 'adult'];
for (const age of ages) {
  test(`${age}: six distinct scenes, three evidenced questions and increasing level demands`, () => {
    const scenes = JSON.parse(readFileSync(new URL(`../../${age}-exercises/v1/scenes.json`, import.meta.url)));
    assert.equal(scenes.length, 30);
    for (let level = 1; level <= 5; level++) {
      const pool = scenes.filter(scene => scene.difficultyLevel === level);
      assert.equal(pool.length, 6);
      assert.equal(new Set(pool.map(scene => scene.description)).size, 6);
      assert.equal(new Set(pool.map(scene => scene.theme)).size, 6);
      for (const scene of pool) {
        assert.equal(scene.mode, 'assessment'); assert.equal(scene.questions.length, 3);
        assert.deepEqual(new Set(scene.questions.map(q => q.questionType)), new Set(['detail', 'spatial', 'sequence']));
        for (const q of scene.questions) {
          assert.equal(new Set(q.options).size, 4);
          assert.match(q.correctAnswer, /^[A-D]$/u);
          assert.ok(scene.description.includes(q.evidence));
          assert.ok(q.options.every(value => value.trim()));
          assert.equal(q.demandLevel, level);
          if (q.questionType === 'detail')
            assert.equal(new Set(q.options.map(option => option.split(' ').slice(1).join(' '))).size, 1,
              'Count distractors must describe the same object, not unrelated items');
        }
      }
      if (level > 1) {
        for (const scene of pool) {
          const earlier = scenes.find(row => row.difficultyLevel === level - 1 && row.theme === scene.theme);
          assert.ok(scene.description.length > earlier.description.length);
          assert.notDeepEqual(scene.questions.map(q => q.questionText), earlier.questions.map(q => q.questionText));
        }
      }
    }
  });
}

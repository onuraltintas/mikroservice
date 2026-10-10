import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('twenty training patches match the approved age, level, count and duration matrix', () => {
  const patches = JSON.parse(readFileSync(new URL('../../content-packs/tachistoscope-settings/v2/settings.json', import.meta.url), 'utf8'));
  assert.equal(patches.length, 20);
  assert.equal(new Set(patches.map(item => item.id)).size, 20);
  const groups = [
    ['Child', [3, 3, 4, 4, 5], 20, 3, 5, 400, 2000, [1500, 1400, 1300, 1200, 1100]],
    ['Teen', [3, 4, 4, 5, 6], 25, 3, 6, 300, 1500, [1000, 900, 800, 700, 600]],
    ['YoungAdult', [4, 4, 5, 5, 6], 30, 4, 6, 250, 1500, [800, 700, 600, 550, 500]],
    ['Adult', [4, 5, 5, 6, 7], 30, 4, 7, 250, 1500, [800, 700, 600, 550, 500]]
  ];
  for (const [age, lengths, firstCount, minLength, maxLength, minDurationMs, maxDurationMs, durations] of groups) {
    for (let i = 0; i < 5; i++) {
      const patch = patches.find(item => item.age === age && item.level === i + 1);
      assert.ok(patch);
      const config = patch.configuration.engineConfig;
      assert.deepEqual(config.content, { type: 'number', count: firstCount + i * 5, length: lengths[i] });
      assert.equal(config.timing.durationMs, durations[i]);
      assert.deepEqual(config.adaptive, { enabled: true, minLength, maxLength, minDurationMs, maxDurationMs });
      const expected = structuredClone(patch.previousConfiguration);
      expected.engineConfig.content = config.content;
      expected.engineConfig.adaptive = config.adaptive;
      assert.deepEqual(patch.configuration, expected, 'Only content count/length and adaptation may change');
    }
  }
});

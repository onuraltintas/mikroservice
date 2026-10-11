import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('Text Fading covers all four ages and five levels with four text-owned questions', () => {
  const settings = JSON.parse(readFileSync(new URL('../../content-packs/text-fading/v1/settings.json', import.meta.url)));
  assert.equal(settings.length, 20);
  assert.equal(new Set(settings.map(item => item.id)).size, 20);
  assert.equal(settings.filter(item => item.previousConfiguration).length, 11);
  const speeds = { child: [60,80,100,120,140], teen: [150,180,210,240,270],
    'young-adult': [180,210,240,270,300], adult: [200,230,260,290,320] };
  for (const [age, values] of Object.entries(speeds)) {
    const rows = settings.filter(item => item.age === age).sort((a,b) => a.level-b.level);
    assert.deepEqual(rows.map(item => item.level), [1,2,3,4,5]);
    assert.deepEqual(rows.map(item => item.configuration.engineConfig.fading.speedWpm), values);
    for (const item of rows) {
      const config = item.configuration.engineConfig;
      assert.equal(config.readingPurpose, 'evaluation');
      assert.equal(config.content.source, 'reading_text');
      assert.equal(config.content.questionCount, 4);
      assert.equal(config.timing.timeLimitSec, 0);
      if (item.previousConfiguration) {
        assert.deepEqual(config.fading, item.previousConfiguration.engineConfig.fading);
      }
    }
  }
});

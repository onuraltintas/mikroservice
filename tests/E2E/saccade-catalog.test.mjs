import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const ages = {
  child: { interval: [900,850,800,750,700], seconds: [40,45,50,55,60] },
  teen: { interval: [850,800,750,700,650], seconds: [45,50,60,70,80] },
  'young-adult': { interval: [800,750,700,650,600], seconds: [50,60,70,80,90] },
  adult: { interval: [850,800,750,700,650], seconds: [50,60,70,80,90] }
};
for (const [age, expected] of Object.entries(ages)) {
  test(`${age} Saccade has exactly the five approved timing pairs`, () => {
    const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
    const rows = catalog.filter(item => item.type === 'Saccade').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
    assert.equal(rows.length, 5);
    for (const [index, row] of rows.entries()) {
      assert.equal(row.difficultyLevel, index + 1);
      assert.equal(row.configuration.engineType, 'motion_path');
      const config = row.configuration.engineConfig;
      assert.equal(config.mode, 'saccade');
      assert.equal(config.timing.holdMs, expected.interval[index]);
      assert.equal(config.timing.durationSeconds, expected.seconds[index]);
      assert.equal(config.content.type, 'number');
      assert.equal(config.content.pointSize, 44);
    }
  });
}

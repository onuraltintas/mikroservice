import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('Fixation has approved age/level rounds and letter bounds without a misleading time limit', () => {
  const ages = {
    child: { base: 20, letters: [1,1,2,2,2], max: 2 },
    teen: { base: 25, letters: [1,2,2,3,3], max: 3 },
    'young-adult': { base: 30, letters: [2,2,3,3,4], max: 4 },
    adult: { base: 35, letters: [2,3,3,4,4], max: 4 }
  };
  for (const [age, expected] of Object.entries(ages)) {
    const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
    const rows = catalog.filter(item => item.type === 'Fixation').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
    assert.equal(rows.length, 5);
    for (const [index, row] of rows.entries()) {
      const config = row.configuration.engineConfig;
      assert.equal(config.content.points, expected.base + index * 5);
      assert.equal(config.content.peripheralCount, expected.letters[index]);
      assert.equal(config.content.maxPeripheralCount, expected.max);
      assert.equal(config.content.pointSize, 44);
      assert.equal(config.content.type, 'letter');
      assert.equal(config.timing.holdMs, (age === 'child' ? [1000,900,850,800,750] : [900,850,800,750,700])[index]);
      assert.equal(config.timing.durationSeconds, undefined);
      assert.equal(config.mode, 'fixation');
    }
  }
});

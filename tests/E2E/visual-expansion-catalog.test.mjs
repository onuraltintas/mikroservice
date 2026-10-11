import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('Visual Expansion has approved age/level rounds without altering exposure or distance', () => {
  const bases = { child: 30, teen: 35, 'young-adult': 40, adult: 45 };
  for (const [age, base] of Object.entries(bases)) {
    const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
    const rows = catalog.filter(item => item.type === 'VisualExpansion').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
    assert.equal(rows.length, 5);
    for (const [index, row] of rows.entries()) {
      const config = row.configuration.engineConfig;
      assert.equal(config.rounds, base + index * 5);
      assert.equal(config.startDegrees, 8);
      assert.equal(config.targetDegrees, (age === 'child' ? 16 : 18) + index * 2);
      assert.equal(config.displayDurationMs, (age === 'child' ? [1200,1100,1000,950,900] : [1100,1000,950,900,850])[index]);
      assert.equal(config.content.stimulusType, 'letter');
      assert.equal(config.mode, 'horizontal');
    }
  }
});

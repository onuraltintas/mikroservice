import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const speeds = { child: [null,140,160,180,200], teen: [160,190,220,260,290],
  'young-adult': [180,210,250,290,320], adult: [180,220,270,310,340] };
for (const [age, tempos] of Object.entries(speeds)) test(`${age}: approved regression levels, masking, tempo and five questions`, () => {
  const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
  const rows = catalog.filter(row => row.type === 'RegressionReduction').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
  assert.deepEqual(rows.map(row => row.difficultyLevel), age === 'child' ? [2,3,4,5] : [1,2,3,4,5]);
  for (const row of rows) {
    const level = row.difficultyLevel, config = row.configuration.engineConfig;
    assert.equal(config.targetWpm, tempos[level-1]); assert.equal(config.wordDelayMs, undefined);
    assert.equal(config.maskingType, 'trailing'); assert.equal(config.visibleHistoryWords, [2,1,1,0,0][level-1]);
    assert.equal(config.chunkSize, 1); assert.equal(config.content.questionCount, 5);
    assert.equal(config.readingPurpose, 'evaluation');
    assert.ok(config.content.minWordCount > 0 && config.content.maxWordCount >= config.content.minWordCount);
  }
});

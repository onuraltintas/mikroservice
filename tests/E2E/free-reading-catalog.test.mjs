import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';

const bands = {
  child: [[150,200],[200,275],[275,350],[350,450],[450,550]],
  teen: [[200,300],[300,400],[400,500],[500,650],[650,800]],
  'young-adult': [[300,400],[400,550],[550,700],[700,850],[850,1000]],
  adult: [[350,450],[450,600],[600,800],[800,1000],[1000,1200]]
};
for (const [age, lengths] of Object.entries(bands)) test(`${age}: five untimed question-free practice levels`, () => {
  const rows = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)))
    .filter(row => row.type === 'FreeReading');
  assert.equal(rows.length, 5);
  for (let level=1; level<=5; level++) {
    const row = rows.find(row => row.difficultyLevel === level);
    assert.ok(row);
    const config = row.configuration.engineConfig;
    assert.equal(config.readingPurpose, 'practice');
    assert.equal(config.content.minWordCount, lengths[level-1][0]);
    assert.equal(config.content.maxWordCount, lengths[level-1][1]);
    assert.equal(config.timing.minReadingTimeMs, 10000);
    assert.equal(config.timing.maxReadingTimeMs, 0);
    assert.ok(config.timing.suggestedMinReadingTimeMs > 0);
    assert.ok(config.timing.suggestedMaxReadingTimeMs >= config.timing.suggestedMinReadingTimeMs);
  }
});
test('all twenty cells have five real paragraph-complete texts', () => {
  const path = new URL('../../content-packs/free-reading/v1/content.json', import.meta.url);
  assert.ok(existsSync(path), 'Free Reading content package is required');
  const content = JSON.parse(readFileSync(path));
  assert.equal(content.length,100);
  assert.equal(new Set(content.map(row=>row.id)).size,100);
  for (const [age,lengths] of Object.entries(bands)) for(let level=1;level<=5;level++) {
    const texts = content.filter(row=>row.age === age && row.level === level);
    assert.equal(texts.length,5);
    assert.equal(new Set(texts.map(row=>row.content)).size,5);
    for(const row of texts) {
      const source=readFileSync(new URL(`../../content-packs/free-reading/v1/${row.source}`,import.meta.url),'utf8').trim().replace(/\r\n/g,'\n');
      assert.ok(source.startsWith(row.content));
      assert.match(row.content, /[.!?]$/u);
      assert.equal(row.wordCount,row.content.trim().split(/\s+/u).length);
      assert.ok(row.wordCount>=lengths[level-1][0] && row.wordCount<=lengths[level-1][1]);
      assert.equal(row.questions,undefined);
    }
  }
});

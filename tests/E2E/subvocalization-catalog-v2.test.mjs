import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const ages = { child: [null,null,null,160,180], teen: [null,null,210,240,270],
  'young-adult': [null,210,240,270,300], adult: [null,230,260,290,320] };
for (const [age, speeds] of Object.entries(ages)) test(`${age}: approved inner speech levels, counting, groups and five questions`, () => {
  const rows = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)))
    .filter(row => row.type === 'SubvocalizationReduction').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
  assert.deepEqual(rows.map(row => row.difficultyLevel), speeds.flatMap((speed,index) => speed ? [index+1] : []));
  for (const row of rows) {
    const c = row.configuration.engineConfig;
    assert.equal(c.targetWpm, speeds[row.difficultyLevel-1]);
    assert.equal(c.chunkSize, row.difficultyLevel === 5 ? 3 : 2);
    assert.equal(c.displayMode, 'highlight'); assert.equal(c.metronomeEnabled, true);
    assert.equal(c.visualMetronome, true); assert.equal(c.metronomeBpm, 60);
    assert.equal(c.readingPurpose, 'evaluation'); assert.equal(c.content.questionCount, 5);
    assert.ok(c.content.minWordCount > 0 && c.content.maxWordCount >= c.content.minWordCount);
  }
});

test('thirteen settings each have three complete source texts and five grounded questions', () => {
  const settings = JSON.parse(readFileSync(new URL('../../content-packs/subvocalization/v2/settings.json', import.meta.url)));
  const texts = JSON.parse(readFileSync(new URL('../../content-packs/subvocalization/v2/content.json', import.meta.url)));
  assert.equal(settings.length, 13); assert.equal(texts.length, 39);
  assert.equal(settings.filter(row => row.previousConfiguration !== null).length, 6);
  assert.equal(new Set(texts.map(row => row.id)).size, 39);
  for (const setting of settings) {
    const rows = texts.filter(row => row.exerciseId === setting.id);
    assert.equal(rows.length, 3); assert.equal(new Set(rows.map(row => row.content)).size, 3);
    for (const row of rows) {
      assert.equal(row.ageGroupId, setting.ageGroupId); assert.equal(row.level, setting.level);
      assert.equal(row.wordCount, row.content.trim().split(/\s+/u).length);
      assert.ok(row.wordCount >= setting.configuration.engineConfig.content.minWordCount);
      assert.ok(row.wordCount <= setting.configuration.engineConfig.content.maxWordCount);
      assert.ok(/[.!?]$/u.test(row.content)); assert.equal(row.questions.length, 5);
      const original = readFileSync(new URL(`../../content-packs/grouping/v1/${row.source}`, import.meta.url), 'utf8').trim();
      assert.ok(original.replace(/\r/g,'').startsWith(row.content.replace(/\r/g,'')));
      for (const question of row.questions) assert.ok(row.content.includes(question.evidence));
    }
  }
});

test('program patches preserve daily counts and regression slots while adding counting sessions', () => {
  const programs = JSON.parse(readFileSync(new URL('../../content-packs/subvocalization/v2/programs.json', import.meta.url)));
  assert.equal(programs.length, 13);
  for (const row of programs) {
    const days = pattern => Object.entries(pattern).filter(([key]) => key.startsWith('week')).flatMap(([,week]) => Object.values(week));
    const before = days(row.previousPattern), after = days(row.pattern);
    assert.equal(after.length, 28); assert.deepEqual(after.map(day => day.length), before.map(day => day.length));
    assert.deepEqual(after.flatMap((day,index) => day.some(task => task.type === 'SubvocalizationReduction') ? [index+1] : []), [23,28]);
    for (const index of [20,25]) assert.deepEqual(after[index], before[index]);
    assert.ok(after.flat().filter(task => task.type === 'SubvocalizationReduction').every(task => task.difficulty === row.level));
  }
});

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

test('every regression record has three distinct complete texts with five grounded questions', () => {
  const settings = JSON.parse(readFileSync(new URL('../../content-packs/regression/v2/settings.json', import.meta.url)));
  const content = JSON.parse(readFileSync(new URL('../../content-packs/regression/v2/content.json', import.meta.url)));
  assert.equal(content.length, 57);
  assert.equal(new Set(content.map(row => row.id)).size, 57);
  for (const config of settings) {
    const texts = content.filter(row => row.exerciseId === config.id);
    assert.equal(texts.length, 3); assert.equal(new Set(texts.map(row => row.content)).size, 3);
    for (const text of texts) {
      assert.equal(text.ageGroupId, config.ageGroupId); assert.equal(text.level, config.level);
      const count = text.content.trim().split(/\s+/u).length;
      assert.equal(count, text.wordCount);
      assert.ok(count >= config.configuration.engineConfig.content.minWordCount);
      assert.ok(count <= config.configuration.engineConfig.content.maxWordCount);
      const original = readFileSync(new URL(`../../content-packs/grouping/v1/${text.source}`, import.meta.url), 'utf8').trim();
      assert.ok(original.replace(/\r/g,'').startsWith(text.content.replace(/\r/g,'')));
      assert.ok(/[.!?]$/u.test(text.content)); assert.equal(text.questions.length, 5);
      assert.deepEqual(text.questions.map(q => q.correctAnswer).sort(), ['A','A','B','C','D']);
      for (const q of text.questions) {
        assert.equal(new Set([q.optionA,q.optionB,q.optionC,q.optionD]).size, 4);
        assert.ok(text.content.includes(q.evidence));
      }
    }
  }
});

test('program patch preserves task counts, excludes child level one and introduces two late-week sessions', () => {
  const programs = JSON.parse(readFileSync(new URL('../../content-packs/regression/v2/programs.json', import.meta.url)));
  assert.equal(programs.length, 19);
  for (const program of programs) {
    assert.ok(!(program.ageGroupId.endsWith('001') && program.level === 1));
    const oldDays = Object.entries(program.previousPattern).filter(([name]) => name.startsWith('week')).flatMap(([,week]) => Object.values(week));
    const days = Object.entries(program.pattern).filter(([name]) => name.startsWith('week')).flatMap(([,week]) => Object.values(week));
    assert.equal(days.length, 28); assert.deepEqual(days.map(day => day.length), oldDays.map(day => day.length));
    const changed = days.flatMap((day,index) => day.some(task => task.type === 'RegressionReduction') ? [index+1] : []);
    assert.deepEqual(changed, [21,26]);
    assert.ok(days.flat().filter(task => task.type === 'RegressionReduction').every(task => task.difficulty === program.level));
  }
});

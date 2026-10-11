import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';

const bands = { child:{4:[180,240],5:[240,300]},teen:{3:[360,440],4:[450,540],5:[560,680]},
  'young-adult':{3:[580,640],4:[680,750],5:[770,850]},adult:{3:[480,540],4:[580,650],5:[690,800]} };
const read = path => JSON.parse(readFileSync(new URL(path, import.meta.url),'utf8'));
for (const [age, levels] of Object.entries(bands)) test(`${age} RSVP keeps levels and exposure but adds bounded five-question selections`,()=>{
  const rows=read(`../../content-packs/${age}-exercises/v1/catalog.json`).filter(row=>row.type==='RSVP');
  assert.deepEqual(rows.map(row=>row.difficultyLevel),Object.keys(levels).map(Number));
  for (const row of rows) {
    const config=row.configuration.engineConfig,[min,max]=levels[row.difficultyLevel];
    assert.equal(config.content.minWordCount,min);assert.equal(config.content.maxWordCount,max);
    assert.equal(config.content.questionCount,5);assert.equal(config.readingPurpose,'evaluation');
    assert.equal(config.visuals.showFixation,true);assert.equal(config.adaptive.enabled,false);
    assert.equal(config.timing.intervalMs,0);
    assert.ok((300+min*config.timing.durationMs)/1000>=90);
    assert.ok((300+max*config.timing.durationMs)/1000<=181);
  }
});
test('all eleven cells have two distinct real selections and five evidenced questions without modifying sources',()=>{
  assert.ok(existsSync(new URL('../../content-packs/rsvp/v2/content.json',import.meta.url)),'RSVP content package is missing');
  const texts=read('../../content-packs/rsvp/v2/content.json'),settings=read('../../content-packs/rsvp/v2/settings.json');
  const source=read('../../content-packs/comprehension/v1/content.json');
  assert.equal(settings.length,11);assert.equal(texts.length,22);assert.equal(new Set(texts.map(text=>text.id)).size,22);
  const questions=[];
  for (const row of texts) {
    const original=source.find(text=>text.id===row.sourceTextId); assert.ok(original);assert.equal(row.content,original.content);
    const [min,max]=bands[row.age][row.level];assert.ok(row.wordCount>=min&&row.wordCount<=max);
    assert.equal(row.wordCount,row.content.trim().split(/\s+/u).length);
    assert.equal(row.questions.length,5);
    for (const q of row.questions) {
      assert.ok(row.content.includes(q.evidence));assert.ok(['A','B','C','D'].includes(q.correctAnswer));
      assert.equal(new Set([q.optionA,q.optionB,q.optionC,q.optionD]).size,4);questions.push(q.id);
    }
  }
  assert.equal(new Set(questions).size,110);
  for (const setting of settings) {
    const cell=texts.filter(text=>text.exerciseId===setting.id);
    assert.equal(cell.length,2);assert.equal(new Set(cell.map(text=>text.content)).size,2);
  }
});

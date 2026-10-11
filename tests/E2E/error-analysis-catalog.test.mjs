import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

test('prepared replacements avoid known valid alternative words',()=>{
  const pairs=JSON.parse(readFileSync(new URL('../../content-packs/error-analysis/v1/pairs.json',import.meta.url)));
  for(const pair of pairs) assert.ok(!['kıssa','gözlemediler'].includes(pair.wrong), `Ambiguous valid word: ${pair.wrong}`);
});

const bands = { child:[[180,240],[240,300]],teen:[[240,320],[320,400]],'young-adult':[[300,380],[380,460]],adult:[[340,420],[420,500]] };
for(const [age, lengths] of Object.entries(bands)) test(`${age} prepared proofreading covers levels four and five`,()=>{
  const catalog=JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`,import.meta.url)));
  const rows=catalog.filter(row=>row.type==='ErrorAnalysis'); assert.equal(rows.length,2);
  for(const row of rows) {
    const config=row.configuration.engineConfig, band=lengths[row.difficultyLevel-4];
    assert.equal(config.content.minWordCount,band[0]); assert.equal(config.content.maxWordCount,band[1]);
    assert.equal(Object.keys(config.errorsByTextId).length,5);
    for(const errors of Object.values(config.errorsByTextId)) {
      assert.equal(errors.length,config.errorCount);
      assert.equal(new Set(errors.map(error=>error.wordIndex)).size,errors.length);
      if(row.difficultyLevel===5) assert.ok(errors.filter(error=>error.errorType==='transposition').length>=2);
    }
  }
});
test('all eight cells have five real texts with exact, explained and unambiguous prepared targets',()=>{
  const path=new URL('../../content-packs/error-analysis/v1/content.json',import.meta.url);
  assert.ok(existsSync(path),'Prepared Error Analysis package is missing');
  const texts=JSON.parse(readFileSync(path)); assert.equal(texts.length,40);
  const pairs=JSON.parse(readFileSync(new URL('../../content-packs/error-analysis/v1/pairs.json',import.meta.url)));
  assert.equal(new Set(texts.map(row=>row.id)).size,40);
  for(const row of texts) {
    const tokens=row.content.trim().split(/\s+/u),[min,max]=bands[row.age][row.level-4];
    const source=readFileSync(new URL(row.source,new URL('../../content-packs/error-analysis/v1/',import.meta.url)),'utf8').trim().replace(/\r\n/g,'\n');
    assert.equal(createHash('sha256').update(source).digest('hex'),row.sourceSha256);
    assert.ok(source.startsWith(row.content));
    assert.equal(tokens.length,row.wordCount); assert.ok(row.wordCount>=min&&row.wordCount<=max);
    for(const error of row.errors) {
      assert.equal(tokens[error.wordIndex],error.originalWord); assert.notEqual(error.originalWord,error.errorWord);
      assert.ok(error.explanation.trim());
      const original=error.originalWord.replace(/^[^\p{L}]+|[^\p{L}]+$/gu,'').toLocaleLowerCase('tr');
      const wrong=error.errorWord.replace(/^[^\p{L}]+|[^\p{L}]+$/gu,'').toLocaleLowerCase('tr');
      assert.ok(pairs.some(pair=>pair.original===original&&pair.wrong===wrong&&pair.type===error.errorType));
    }
  }
});

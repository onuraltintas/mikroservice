import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const url = new URL('./short-content.json', import.meta.url);
const records = fs.existsSync(url) ? JSON.parse(fs.readFileSync(url, 'utf8')) : [];
test('child fading has three short texts at every level with four linked questions', () => {
  assert.equal(records.length, 15);
  for (let level = 1; level <= 5; level++) {
    const texts = records.filter(x => x.level === level);
    assert.equal(texts.length, 3);
    for (const text of texts) {
      const count = text.content.trim().split(/\s+/u).length;
      const wpm = [60, 80, 100, 120, 140][level - 1];
      assert.ok(count >= wpm * 1.5 && count <= wpm * 2, `${level}: ${count}`);
      assert.equal(text.questions.length, 4);
      assert.equal(new Set(text.questions.map(q => q.question)).size, 4);
      for (const q of text.questions) {
        assert.ok(['A', 'B', 'C', 'D'].includes(q.correct));
        assert.equal(q.options.length, 4);
        assert.ok(text.content.includes(q.evidence));
      }
    }
  }
});

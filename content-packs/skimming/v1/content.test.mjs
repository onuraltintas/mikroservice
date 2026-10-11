import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildSkimmingPack } from './generate.mjs';

test('covers existing 3–5 levels with increasing text demand and four evidence-linked questions', () => {
  const { settings, content } = buildSkimmingPack();
  assert.equal(settings.length, 12);
  assert.equal(content.length, 36);
  assert.equal(new Set([...settings, ...content, ...content.flatMap(text => text.questions)].map(row => row.id)).size, 192);
  for (const age of ['child', 'teen', 'young-adult', 'adult']) {
    const rows = settings.filter(row => row.age === age);
    assert.deepEqual(rows.map(row => row.level), [3, 4, 5]);
    let priorDemand = 0;
    for (const row of rows) {
      const { content: band, timing } = row.configuration.engineConfig;
      assert.equal(band.questionCount, 4);
      assert.equal(row.configuration.engineConfig.readingPurpose, 'evaluation');
      assert(timing.minReadingTimeMs >= 10000 && timing.minReadingTimeMs < timing.maxReadingTimeMs);
      const demand = band.minWordCount / (timing.maxReadingTimeMs / 1000);
      assert(demand > priorDemand);
      priorDemand = demand;
      const texts = content.filter(text => text.exerciseId === row.id);
      assert.equal(texts.length, 3);
      assert.equal(new Set(texts.map(text => text.content)).size, 3);
      for (const text of texts) {
        assert.equal(text.ageGroupId, row.ageGroupId);
        assert.equal(text.level, row.level);
        assert.equal(text.wordCount, text.content.trim().split(/\s+/u).length);
        assert(text.wordCount >= band.minWordCount && text.wordCount <= band.maxWordCount);
        assert.equal(text.questions.length, 4);
        assert.equal(new Set(text.questions.map(question => question.questionText)).size, 4);
        assert.deepEqual(text.questions.map(question => question.correctAnswer), ['A', 'B', 'C', 'D']);
        for (const question of text.questions) {
          assert.equal(question.type, 1);
          assert(text.content.includes(question.evidence));
          assert(question.explanation && question.evidence.length > 20);
          assert.equal(new Set(['A','B','C','D'].map(option => question[`option${option}`])).size, 4);
        }
      }
    }
  }
});

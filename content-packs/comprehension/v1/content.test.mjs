import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';

const read = name => JSON.parse(readFileSync(new URL(name, import.meta.url), 'utf8'));
const settings = read('./settings.json');
const words = value => value.trim().split(/\s+/u).length;

test('every approved exercise has two distinct, age/level/band-matching texts and the exact question count', () => {
  assert.ok(existsSync(new URL('./content.json', import.meta.url)), 'Comprehension content manifest is missing');
  const content = read('./content.json');
  assert.equal(settings.length, 40);
  assert.equal(content.length, 80);
  assert.equal(new Set(content.map(row => row.id)).size, 80);
  for (const setting of settings) {
    const texts = content.filter(row => row.exerciseId === setting.id);
    assert.equal(texts.length, 2, setting.id);
    assert.equal(new Set(texts.map(row => row.content)).size, 2);
    const band = setting.configuration.engineConfig.content;
    for (const text of texts) {
      assert.equal(text.ageGroupId, setting.ageGroupId);
      assert.equal(text.level, setting.level);
      assert.equal(words(text.content), text.wordCount);
      assert.ok(text.wordCount >= band.minWordCount && text.wordCount <= band.maxWordCount, text.title);
      assert.equal(text.questions.length, band.questionCount);
      assert.match(text.content, /[.!?]$/u, 'A selection must end at a complete paragraph');
      assert.equal(new Set(text.content.split('\n\n')).size, text.content.split('\n\n').length, 'No repeated padding');
    }
  }
});

test('questions have unique options, evidence in their exact selection, explanations and balanced answer keys', () => {
  const content = read('./content.json');
  const questions = content.flatMap(row => row.questions);
  assert.equal(questions.length, 588);
  assert.equal(new Set(questions.map(row => row.id)).size, 588);
  for (const text of content) {
    assert.equal(new Set(text.questions.map(row => row.questionText)).size, text.questions.length);
    const frequencies = ['A', 'B', 'C', 'D'].map(key => text.questions.filter(q => q.correctAnswer === key).length);
    assert.ok(Math.max(...frequencies) - Math.min(...frequencies) <= 1);
    text.questions.forEach((q, order) => {
      assert.equal(q.orderIndex, order);
      assert.ok(q.type >= 1 && q.type <= 3);
      assert.ok(q.bloomLevel >= 1 && q.bloomLevel <= 6);
      assert.ok(q.evidence.length > 12 && text.content.includes(q.evidence), `${text.title}: ${q.questionText}`);
      assert.ok(q.explanation.length > 30);
      const options = ['A', 'B', 'C', 'D'].map(key => q[`option${key}`]);
      assert.equal(new Set(options).size, 4);
      assert.ok(options.every(value => typeof value === 'string' && value.trim().length > 0));
    });
    assert.ok(text.questions.some(q => q.type === 2), 'Every level includes supported inference');
    if (text.level >= 4) assert.ok(text.questions.some(q => q.type === 3), 'Upper levels include evaluation');
  }
});

test('both modes preserve the approved ids, use questions, and do not change placement exercises', () => {
  for (const setting of settings) {
    const catalog = read(`../../${setting.age}-exercises/v1/catalog.json`);
    const old = catalog.find(row => row.id === setting.id);
    assert.ok(old && old.type === 'Comprehension', setting.id);
    assert.deepEqual(setting.previousConfiguration, old.configuration);
    const config = setting.configuration.engineConfig;
    assert.ok(['learning', 'evaluation'].includes(config.comprehensionMode));
    assert.equal(config.readingPurpose, 'evaluation');
    assert.equal(config.timing.maxReadingTimeMs, 0);
    const peers = settings.filter(s => s.age === setting.age && s.level === setting.level);
    assert.deepEqual(peers.map(s => s.configuration.engineConfig.comprehensionMode).sort(), ['evaluation', 'learning']);
  }
});

test('source provenance is verifiable and selections are complete, unchanged source paragraphs', () => {
  for (const row of read('./content.json')) {
    const source = readFileSync(new URL(row.source, import.meta.url), 'utf8').trim().replace(/\r\n/g, '\n');
    assert.ok(source.startsWith(row.content), row.title);
    assert.equal(createHash('sha256').update(source).digest('hex'), row.sourceSha256);
  }
});

test('rendered SQL exactly matches its versioned template and immutable payloads', () => {
  const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
  const sql = readFileSync(new URL('./apply.sql', import.meta.url), 'utf8');
  const rendered = template.replace('{{SETTINGS}}', JSON.stringify(settings, null, 2))
    .replace('{{CONTENT}}', JSON.stringify(read('./content.json'), null, 2));
  assert.equal(sql, rendered);
  assert.equal((sql.match(/^BEGIN;$/gm) ?? []).length, 1);
  assert.equal((sql.match(/^COMMIT;$/gm) ?? []).length, 1);
  assert.doesNotMatch(sql, /\b(?:DELETE\s+FROM|TRUNCATE|UPDATE\s+speed_reading\.(?:student|exercise_session|reading_session|program_template))/iu);
});

test('rollback restores only known catalog settings and archives new texts without deleting history', () => {
  const template = readFileSync(new URL('./rollback.template.sql', import.meta.url), 'utf8');
  const sql = readFileSync(new URL('./rollback.sql', import.meta.url), 'utf8');
  assert.equal(sql, template.replace('{{SETTINGS}}', JSON.stringify(settings, null, 2))
    .replace('{{CONTENT}}', JSON.stringify(read('./content.json'), null, 2)));
  assert.equal((sql.match(/^BEGIN;$/gm) ?? []).length, 1);
  assert.equal((sql.match(/^COMMIT;$/gm) ?? []).length, 1);
  assert.match(sql, /SET is_active=false/);
  assert.doesNotMatch(sql, /\b(?:DELETE\s+FROM|TRUNCATE|UPDATE\s+speed_reading\.(?:student|exercise_session|reading_session|program_template))/iu);
});

test('production inventory excludes only the exact approved target rows, not whole tables', () => {
  const sql = readFileSync(new URL('./inventory.sql', import.meta.url), 'utf8');
  const content = read('./content.json');
  const targets = {
    exercises: settings.map(row => row.id), reading_texts: content.map(row => row.id),
    reading_questions: content.flatMap(row => row.questions.map(q => q.id))
  };
  for (const [table, ids] of Object.entries(targets)) {
    assert.ok(sql.includes(`WHEN '${table}' THEN $where$WHERE id NOT IN (${ids.map(id => `'${id}'::uuid`).join(',')})$where$`));
  }
  assert.match(sql, /ELSE '' END/);
  assert.doesNotMatch(sql, /tablename NOT IN|\bDELETE\b|\bUPDATE\b/iu);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { build, TARGETS, ACTOR, wordCount, paragraphs } from './generate.mjs';
import { optionLengthProblem, QUESTIONS_PER_TEXT, surfaceCues, MAX_SURFACE_CUE } from '../../pool-questions/rules.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const read = name => readFileSync(join(directory, name), 'utf8');
const files = ['texts.json', 'questions.json', 'apply.sql', 'rollback.sql'];
const { texts, rows, problems } = build();
const options = row => [row.optionA, row.optionB, row.optionC, row.optionD];
const correct = row => row[`option${row.correctAnswer}`];

test('generated files are current', () => {
  const before = files.map(read);
  execFileSync(process.execPath, [join(directory, 'generate.mjs')]);
  assert.deepEqual(files.map(read), before);
});

test('every slot has one text in its age, level and word range', () => {
  assert.deepEqual(problems, []);
  assert.equal(TARGETS.length, 290);
  assert.equal(texts.length, TARGETS.length);
  assert.equal(new Set(texts.map(text => text.id)).size, texts.length);
  for (const target of TARGETS) {
    const text = texts.find(item => item.slot === target.slot);
    assert.ok(text, `yuva ${target.slot}`);
    assert.equal(text.ageGroupId, target.ageGroupId, `yuva ${target.slot}`);
    assert.equal(text.level, target.level, `yuva ${target.slot}`);
    assert.equal(text.wordCount, wordCount(text.content), `yuva ${target.slot}`);
    assert.ok(text.wordCount >= target.min && text.wordCount <= target.max, `yuva ${target.slot}: ${text.wordCount}`);
    assert.ok(paragraphs(text.content).length >= 3, `yuva ${target.slot}`);
  }
});

test('every text gets exactly eight questions that pass the rules', () => {
  assert.equal(rows.length, TARGETS.length * QUESTIONS_PER_TEXT);
  assert.equal(new Set(rows.map(row => row.id)).size, rows.length);
  for (const text of texts) {
    const own = rows.filter(row => row.readingTextId === text.id);
    assert.equal(own.length, QUESTIONS_PER_TEXT, text.id);
    assert.deepEqual(own.map(row => row.orderIndex), [0, 1, 2, 3, 4, 5, 6, 7], text.id);
    assert.ok(own.every(row => row.difficultyLevel === text.level), text.id);
  }
});

test('the answer cannot be guessed from the options alone', () => {
  for (const row of rows) {
    const list = options(row);
    assert.equal(new Set(list).size, 4, row.id);
    assert.equal(optionLengthProblem([correct(row), ...list.filter(option => option !== correct(row))]), null, row.id);
  }
});

test('no surface strategy finds the answer without reading the question and the text', () => {
  const content = new Map(texts.map(text => [text.id, text.content]));
  const cues = surfaceCues(rows.map(row => ({ options: options(row), answer: 'ABCD'.indexOf(row.correctAnswer),
    text: content.get(row.readingTextId) })));
  for (const [name, value] of Object.entries(cues)) assert.ok(value <= MAX_SURFACE_CUE[name], `${name}: %${(100 * value).toFixed(1)}`);
});

test('answer letters are balanced within every text and across the pack', () => {
  const total = { A: 0, B: 0, C: 0, D: 0 };
  for (const text of texts) {
    const letters = rows.filter(row => row.readingTextId === text.id).map(row => row.correctAnswer);
    for (const letter of 'ABCD') {
      const count = letters.filter(answer => answer === letter).length;
      assert.ok(count >= 1 && count <= 3, `${text.id}: ${letter} ${count}`);
      total[letter] += count;
    }
  }
  for (const letter of 'ABCD') assert.ok(Math.abs(total[letter] - rows.length / 4) <= 15, `${letter} ${total[letter]}`);
});

test('titles are unique within each age group', () => {
  const seen = new Set();
  for (const text of texts) {
    const key = `${text.ageGroupId}|${text.title}`;
    assert.ok(!seen.has(key), text.title);
    seen.add(key);
  }
});

test('SQL is guarded, idempotent and reversible', () => {
  const apply = read('apply.sql'), rollback = read('rollback.sql');
  assert.match(apply, /IF conflicts <> 0 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /WHERE NOT EXISTS \(SELECT 1 FROM speed_reading\.reading_texts t WHERE t\.id = s\.id\)/);
  assert.match(apply, /WHERE NOT EXISTS \(SELECT 1 FROM speed_reading\.reading_questions q WHERE q\.id = s\.id\)/);
  assert.doesNotMatch(apply + rollback, /DELETE FROM|DROP TABLE speed_reading|TRUNCATE/);
  assert.match(rollback, new RegExp(`created_by = '${ACTOR}'`));
  assert.match(rollback, /SET is_deleted = TRUE/);
  assert.match(apply, new RegExp(`deleted_by = '${ACTOR}-rollback'`));
  for (const text of texts) {
    assert.ok(apply.includes(`('${text.id}'::uuid`), text.id);
    assert.ok(rollback.includes(`'${text.id}'::uuid`), text.id);
  }
  for (const row of rows) assert.ok(apply.includes(`('${row.id}'::uuid, '${row.readingTextId}'::uuid`), row.id);
});

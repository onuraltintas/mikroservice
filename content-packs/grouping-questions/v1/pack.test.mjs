import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { paragraphs, pick } from './generate.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const read = name => readFileSync(join(directory, name), 'utf8');
const texts = JSON.parse(readFileSync(join(directory, '../../grouping/v1/content.json'), 'utf8'));
const rows = JSON.parse(read('questions.json'));
const banks = Object.fromEntries(['library', 'garden', 'map'].map(name => [name, JSON.parse(read(`banks/${name}.json`)).questions]));

test('generated files are current', () => {
  const before = ['questions.json', 'apply.sql', 'rollback.sql'].map(read);
  execFileSync(process.execPath, [join(directory, 'generate.mjs')]);
  assert.deepEqual(['questions.json', 'apply.sql', 'rollback.sql'].map(read), before);
});

test('every pack text gets exactly four valid, scorable questions answerable from its own paragraphs', () => {
  assert.equal(texts.length, 60);
  assert.equal(rows.length, 240);
  assert.equal(new Set(rows.map(row => row.id)).size, 240);
  for (const text of texts) {
    const own = rows.filter(row => row.readingTextId === text.id);
    assert.equal(own.length, 4, text.id);
    assert.deepEqual(own.map(row => row.orderIndex), [0, 1, 2, 3]);
    assert.equal(new Set(own.map(row => row.questionText)).size, 4);
    assert.ok(own.some(row => row.type >= 2), `${text.id} needs an inference or evaluation question`);
    const bank = banks[text.source.replace('texts/', '').replace('.txt', '')];
    for (const row of own) {
      const question = bank.find(item => item.key === row.key);
      assert.ok(question.need <= paragraphs(text.content), `${row.key} is not answerable from ${text.id}`);
      assert.equal(row.contentMd5, createHash('md5').update(text.content, 'utf8').digest('hex'));
      assert.equal(row.difficultyLevel, text.level);
    }
  }
});

test('questions follow the reading-content rules', () => {
  for (const bank of Object.values(banks)) {
    assert.equal(new Set(bank.map(question => question.key)).size, bank.length);
    for (const question of bank) {
      assert.ok([1, 2, 3].includes(question.type) && question.bloom >= 1 && question.bloom <= 6, question.key);
      assert.ok(['A', 'B', 'C', 'D'].includes(question.answer), question.key);
      assert.equal(question.options.length, 4);
      assert.equal(new Set(question.options).size, 4, question.key);
      assert.ok(question.options.every(option => option.trim() && !/^[A-D][).]\s/u.test(option)), question.key);
      const lengths = question.options.map(option => option.length);
      const correct = lengths['ABCD'.indexOf(question.answer)];
      assert.ok(!(correct === Math.max(...lengths) && lengths.filter(length => length === correct).length === 1),
        `${question.key}: the correct option must not be the only longest one`);
      assert.ok(question.explanation.trim().length > 20 && question.explanation.length <= 300, question.key);
    }
  }
  const letters = rows.reduce((counts, row) => ({ ...counts, [row.correctAnswer]: (counts[row.correctAnswer] ?? 0) + 1 }), {});
  for (const letter of 'ABCD') assert.ok(letters[letter] >= 40, `answer ${letter} is used ${letters[letter]} times`);
});

test('selection spreads questions over the passage and skips early details in long passages', () => {
  assert.deepEqual(pick(banks.library, 5).map(question => question.key), ['L01', 'L02', 'L04', 'L05']);
  assert.ok(pick(banks.library, 21).every(question => question.need >= 7));
  assert.throws(() => pick(banks.library, 1));
});

test('SQL is guarded, idempotent and reversible', () => {
  const apply = read('apply.sql'), rollback = read('rollback.sql');
  assert.match(apply, /md5\(t\.content\) = c\.content_md5/);
  assert.match(apply, /IF matched <> 60 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /WHERE NOT EXISTS \(SELECT 1 FROM speed_reading\.reading_questions target WHERE target\.id = source\.id\)/);
  for (const row of rows) { assert.ok(apply.includes(`'${row.id}'::uuid`)); assert.ok(rollback.includes(`'${row.id}'::uuid`)); }
  assert.match(rollback, /created_by = 'grouping-questions-v1'/);
  assert.doesNotMatch(rollback, /DELETE FROM/);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { build, loadTexts, TARGETS, ACTOR } from './generate.mjs';
import { optionLengthProblem, QUESTIONS_PER_TEXT, surfaceCues, MAX_SURFACE_CUE } from '../rules.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const read = name => readFileSync(join(directory, name), 'utf8');
const files = ['questions.json', 'apply.sql', 'rollback.sql'];
const { rows, problems } = build();
const options = row => [row.optionA, row.optionB, row.optionC, row.optionD];
const correct = row => row[`option${row.correctAnswer}`];

test('generated files are current', () => {
  const before = files.map(read);
  execFileSync(process.execPath, [join(directory, 'generate.mjs')]);
  assert.deepEqual(files.map(read), before);
});

test('every moved Serbest Okuma text gets exactly eight questions that pass the rules', () => {
  assert.deepEqual(problems, []);
  assert.equal(TARGETS.length, 79);
  assert.equal(rows.length, 79 * QUESTIONS_PER_TEXT);
  assert.equal(new Set(rows.map(row => row.id)).size, rows.length);
  const texts = loadTexts();
  for (const id of TARGETS) {
    const own = rows.filter(row => row.readingTextId === id);
    assert.equal(own.length, QUESTIONS_PER_TEXT, id);
    assert.deepEqual(own.map(row => row.orderIndex), [0, 1, 2, 3, 4, 5, 6, 7], id);
    assert.ok(own.every(row => row.difficultyLevel === texts.get(id).difficulty_level), id);
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
  const texts = loadTexts();
  const cues = surfaceCues(rows.map(row => ({ options: options(row), answer: 'ABCD'.indexOf(row.correctAnswer),
    text: texts.get(row.readingTextId).content })));
  for (const [name, value] of Object.entries(cues)) assert.ok(value <= MAX_SURFACE_CUE[name], `${name}: %${(100 * value).toFixed(1)}`);
});

// Harfler her metinde ikişer planlanır; yalnız sayısal şıklar küçükten büyüğe dizildiği için planlanandan sapabilir.
test('answer letters are balanced within every text and across the pack', () => {
  const total = { A: 0, B: 0, C: 0, D: 0 };
  for (const id of TARGETS) {
    const letters = rows.filter(row => row.readingTextId === id).map(row => row.correctAnswer);
    for (const letter of 'ABCD') {
      const count = letters.filter(answer => answer === letter).length;
      assert.ok(count >= 1 && count <= 3, `${id}: ${letter} ${count}`);
      total[letter] += count;
    }
  }
  for (const letter of 'ABCD') assert.ok(Math.abs(total[letter] - rows.length / 4) <= 5, `${letter} ${total[letter]}`);
});

test('SQL is guarded, idempotent and reversible', () => {
  const apply = read('apply.sql'), rollback = read('rollback.sql');
  assert.match(apply, /md5\(t\.content\) = c\.content_md5/);
  assert.match(apply, /IF matched <> 79 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /IF foreign_questions <> 0 THEN\s+RAISE EXCEPTION/);
  assert.match(apply, /WHERE NOT EXISTS \(SELECT 1 FROM speed_reading\.reading_questions target WHERE target\.id = source\.id\)/);
  assert.doesNotMatch(apply + rollback, /DELETE FROM|DROP TABLE speed_reading|TRUNCATE|UPDATE speed_reading\.reading_texts/);
  assert.match(rollback, new RegExp(`created_by = '${ACTOR}'`));
  assert.match(rollback, /SET is_deleted = TRUE/);
  assert.match(apply, new RegExp(`target\\.deleted_by = '${ACTOR}-rollback'`));
  for (const row of rows) {
    assert.ok(apply.includes(`('${row.id}'::uuid, '${row.readingTextId}'::uuid`), row.id);
    assert.ok(rollback.includes(`'${row.id}'::uuid`), row.id);
  }
});

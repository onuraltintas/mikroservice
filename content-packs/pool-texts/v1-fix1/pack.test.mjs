import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { build, ACTOR, FIELDS } from './generate.mjs';
import { MAX_SURFACE_CUE } from '../../pool-questions/rules.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const read = name => readFileSync(join(directory, name), 'utf8');
const files = ['apply.sql', 'rollback.sql'];
const { pairs, after, cues, problems } = build();

test('generated files are current', () => {
  const before = files.map(read);
  execFileSync(process.execPath, [join(directory, 'generate.mjs')]);
  assert.deepEqual(files.map(read), before);
});

test('four v1 questions are fixed and every fixed question passes the rules', () => {
  assert.deepEqual(problems, []);
  assert.equal(pairs.length, 4);
  for (const { old, row } of pairs) {
    assert.ok(old.id.startsWith('d7b70000-'), old.id);
    assert.equal(row.correctAnswer, old.correctAnswer, 'cevap harfi aynı kalır');
    for (const key of ['readingTextId', 'type', 'bloomLevel', 'difficultyLevel', 'orderIndex', 'paragraph']) assert.equal(row[key], old[key], key);
    assert.ok(FIELDS.some(([key]) => row[key] !== old[key]), old.id);
  }
});

test('pack-wide guess cues stay within the limit', () => {
  assert.equal(after.length, 2320);
  for (const [name, value] of Object.entries(cues)) assert.ok(value <= MAX_SURFACE_CUE[name], `${name} ${value}`);
});

test('SQL only touches the four questions and refuses hand-edited rows', () => {
  for (const [name, actor] of [['apply.sql', ACTOR], ['rollback.sql', `${ACTOR}-rollback`]]) {
    const script = read(name);
    assert.match(script, /^BEGIN;$/m);
    assert.match(script, /^COMMIT;$/m);
    assert.match(script, /IF unexpected <> 0 THEN\s+RAISE EXCEPTION/);
    assert.doesNotMatch(script, /\b(DELETE FROM|DROP (TABLE|SCHEMA)|TRUNCATE|INSERT INTO speed_reading)\b/i);
    assert.equal(script.match(/UPDATE speed_reading\.reading_questions/g).length, 1);
    assert.match(script, new RegExp(`updated_by = '${actor}'`));
    for (const { old } of pairs) assert.ok(script.includes(`'${old.id}'::uuid`), old.id);
  }
});

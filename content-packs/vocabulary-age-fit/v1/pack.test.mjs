import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { render } from './generate.mjs';

const { words } = JSON.parse(readFileSync(new URL('./words.json', import.meta.url), 'utf8'));
const file = name => readFileSync(new URL(name, import.meta.url), 'utf8').replace(/\r\n/g, '\n');
const CHILD = '10000000-0000-0000-0000-000000000001';
const TEEN = '10000000-0000-0000-0000-000000000002';
// Words per session of the level 4-5 vocabulary exercises (content-packs/vocabulary/v1).
const NEEDED = { [CHILD]: { 4: 14, 5: 16 }, [TEEN]: { 4: 16, 5: 18 } };

test('Çocuk and Genç each get a full, unambiguous level 4 and 5 pool', () => {
  assert.equal(new Set(words.map(row => row.id)).size, words.length);
  for (const [age, levels] of Object.entries(NEEDED)) {
    for (const [level, needed] of Object.entries(levels)) {
      const pool = words.filter(row => row.ageGroupId === age && row.level === Number(level));
      assert.ok(pool.length >= needed + 6, `${age} L${level}: ${pool.length} words leave little variety`);
      const key = text => text.trim().toLocaleLowerCase('tr-TR');
      assert.equal(new Set(pool.map(row => key(row.word))).size, pool.length, `duplicate word in ${age} L${level}`);
      assert.equal(new Set(pool.map(row => key(row.definition))).size, pool.length, `duplicate definition in ${age} L${level}`);
    }
  }
  assert.deepEqual([...new Set(words.map(row => row.ageGroupId))].sort(), [CHILD, TEEN]);
});

test('the new words are plain Turkish with a full sentence as definition', () => {
  for (const row of words) {
    assert.match(row.definition, /^[A-ZÇĞİÖŞÜ].{15,}\.$/u, row.word);
    assert.ok(!row.definition.toLocaleLowerCase('tr-TR').includes(row.word.toLocaleLowerCase('tr-TR')), `${row.word} explains itself`);
    assert.ok(row.category.trim().length > 0);
    assert.doesNotMatch(row.word, /izm$|loji$|asyon$|ite$/u, `${row.word} looks like academic jargon`);
  }
});

test('apply.sql matches the generator, checks its baseline and keeps history', () => {
  const sql = file('apply.sql');
  assert.equal(sql, render());
  assert.match(sql, /Vocabulary baseline changed/);
  assert.match(sql, /already applied/);
  assert.match(sql, /Vocabulary pool too small after age fit/);
  assert.doesNotMatch(sql, /\b(DELETE FROM|TRUNCATE|DROP TABLE)\b/i);
  assert.doesNotMatch(sql, /lesson|curriculum|coaching/i, 'the pack touches only Speed Reading vocabulary');
});

test('rollback returns the shared pool and soft-deletes only the pack rows', () => {
  const sql = file('rollback.sql');
  assert.match(sql, /vocabulary_age_fit_v1_backup/);
  assert.match(sql, /SET is_deleted = TRUE/);
  assert.match(sql, /WHERE created_by = 'vocabulary-age-fit-v1'/);
  assert.doesNotMatch(sql, /DELETE FROM|TRUNCATE/i);
});

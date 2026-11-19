import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { render } from './generate.mjs';

const pack = JSON.parse(readFileSync(new URL('./words.json', import.meta.url), 'utf8'));
const file = name => readFileSync(new URL(name, import.meta.url), 'utf8').replace(/\r\n/g, '\n');
const CHILD = '10000000-0000-0000-0000-000000000001';
const key = text => text.trim().toLocaleLowerCase('tr-TR');

test('Çocuk gets a full, unambiguous level 3 pool of its own words', () => {
  const words = pack.words;
  assert.equal(new Set(words.map(row => row.id)).size, words.length);
  assert.ok(words.every(row => row.ageGroupId === CHILD && row.level === 3));
  assert.ok(words.length >= 12 + 6);
  assert.equal(new Set(words.map(row => key(row.word))).size, words.length);
  assert.equal(new Set(words.map(row => key(row.definition))).size, words.length);
  const kept = pack.childKeepWords.map(([, word]) => key(word));
  assert.ok(words.every(row => !kept.includes(key(row.word))), 'a new word repeats a kept word');
});

test('definitions are plain sentences that do not repeat the word', () => {
  for (const row of pack.words) {
    assert.match(row.definition, /^[A-ZÇĞİÖŞÜ].{15,}\.$/u, row.word);
    assert.ok(!row.definition.toLocaleLowerCase('tr-TR').includes(row.word.toLocaleLowerCase('tr-TR')), row.word);
  }
});

test('business, economy, HR, management and law terms are adult-only; Çocuk keeps geography', () => {
  assert.deepEqual(pack.adultOnlyCategories.slice().sort(),
    ['Ekonomi', 'Hukuk', 'Insan Kaynaklari', 'Is Dunyasi', 'Yonetim', 'İş Dünyası'].sort());
  assert.deepEqual(pack.childKeepCategories, ['Cografya']);
});

test('apply.sql matches the generator; rollback restores and keeps history', () => {
  const sql = file('apply.sql');
  assert.equal(sql, render());
  assert.match(sql, /Vocabulary baseline changed/);
  assert.match(sql, /already applied/);
  assert.match(sql, /Çocuk still sees business or economy words/);
  assert.doesNotMatch(sql, /\b(DELETE FROM|TRUNCATE|DROP TABLE)\b/i);
  const rollback = file('rollback.sql');
  assert.match(rollback, /vocabulary_age_fit_v2_backup/);
  assert.doesNotMatch(rollback, /DELETE FROM|TRUNCATE/i);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// Review of PR 135 (S16): the plan wizard checks the routine answers on the client with the server's own sentences
// (StudyRoutine.cs), so the student meets the same message before and after saving. Every user-facing string literal of the server
// must appear in the client mirror, apart from the documented exceptions below.
const root = new URL('../../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

/** Sentences the client never needs, with the reason. */
const EXCEPTIONS = new Map([
  ['Günlük düzen sorularını yanıtla.', 'the client always sends the answers object'],
  ['TYT ve AYT ağırlığını seç.', 'the client sends only the enum names it offers'],
  ['Tempo Rahat, Dengeli ya da Yoğun olabilir.', 'the client sends only the enum names it offers'],
  ['{field} tam saat ya da buçuk olmalı (00:00–24:00).', 'unreachable: every time choice is a half hour from 00:00 to 24:00'],
  // Field names used only in the unreachable "tam saat ya da buçuk" sentence above.
  ['Akşam yemeği saatin', 'only in the unreachable half-hour sentence'],
  ['Kurs günlerindeki başlama saatin', 'only in the unreachable half-hour sentence'],
  ['Okuldan sonra başlama saatin', 'only in the unreachable half-hour sentence'],
  ['Okulun giriş saati', 'only in the unreachable half-hour sentence']
]);

function literals(source) {
  // Comments are not shown to anyone: drop whole comment lines and a trailing "// …" outside a string.
  const code = source.split('\n').filter(line => !/^\s*\/\//.test(line)).map(line => {
    for (let at = line.indexOf('//'); at >= 0; at = line.indexOf('//', at + 2))
      if ((line.slice(0, at).match(/"/g) ?? []).length % 2 === 0) return line.slice(0, at);
    return line;
  }).join('\n');
  const found = [];
  for (const match of code.matchAll(/(\$?)"((?:[^"\\]|\\.)*)"/g)) {
    const [, interpolated, text] = match;
    // User-facing: Turkish words with a space (not identifiers such as nameof targets).
    if (!/\s/.test(text) || !/[a-zçğıöşü]/i.test(text)) continue;
    found.push({ text, interpolated: interpolated === '$' });
  }
  return found;
}

test('every StudyRoutine sentence is in the client mirror (study-routine.ts)', async () => {
  const server = await read('services/coaching-service/Coaching.Application/StudyPlanning/StudyRoutine.cs');
  const client = await read('clients/admin-panel/src/app/features/coaching-portal/pages/study-routine.ts');
  const strings = literals(server);
  assert.ok(strings.length > 30, `expected the server's sentences, found ${strings.length}`);
  const missing = [];
  for (const { text, interpolated } of strings) {
    if (EXCEPTIONS.has(text)) continue;
    if (interpolated) {
      // The fixed parts around each {placeholder} must be there (the client uses ${…} in the same places).
      for (const part of text.split(/\{[^}]*\}/).map(x => x).filter(x => x.trim()))
        if (!client.includes(part)) missing.push(`${text} → "${part}"`);
    } else if (!client.includes(text)) missing.push(text);
  }
  assert.deepEqual(missing, [], `sentences missing from study-routine.ts:\n${missing.join('\n')}`);
});

test('the documented exceptions still exist on the server (remove stale ones)', async () => {
  const server = await read('services/coaching-service/Coaching.Application/StudyPlanning/StudyRoutine.cs');
  const texts = new Set(literals(server).map(x => x.text));
  const stale = [...EXCEPTIONS.keys()].filter(text => !texts.has(text));
  assert.deepEqual(stale, []);
});

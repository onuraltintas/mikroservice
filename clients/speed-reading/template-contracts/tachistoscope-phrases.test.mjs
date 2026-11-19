import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Live test 19.11.2026: one phrase list for Kısa Gösterim level 5; the client preview keeps an identical copy.
const server = readFileSync(new URL('../../../services/speed-reading-service/SpeedReading.Domain/Sessions/TachistoscopePhrases.cs', import.meta.url), 'utf8');
const client = readFileSync(new URL('../src/app/features/student/exercises/universal-player/engines/tachistoscope-phrases.ts', import.meta.url), 'utf8');
const serverList = [...server.split('All =')[1].split('];')[0].matchAll(/"([^"]+)"/g)].map(match => match[1]);
const clientList = [...client.split('= [')[1].split('];')[0].matchAll(/'([^']+)'/g)].map(match => match[1]);

test('server and client phrase lists are identical', () => {
  assert.ok(serverList.length >= 200);
  assert.deepEqual(clientList, serverList);
});

test('every phrase is two plain words and every level 5 length (6-16) has at least twelve', () => {
  for (const phrase of serverList) assert.match(phrase, /^\p{L}+ \p{L}+$/u, phrase);
  for (let length = 6; length <= 16; length++)
    assert.ok(serverList.filter(phrase => phrase.length === length).length >= 12, `length ${length}`);
});

test('random word pairing is gone', () => {
  assert.doesNotMatch(readFileSync(new URL('../../../services/speed-reading-service/SpeedReading.Domain/Sessions/TachistoscopeContent.cs', import.meta.url), 'utf8'), /BuildPhrasePool/);
  assert.match(readFileSync(new URL('../../../services/speed-reading-service/SpeedReading.Infrastructure/Persistence/OwnedSpeedReadingExerciseSessions.cs', import.meta.url), 'utf8'), /TachistoscopePhrases\.Pool\(/);
});

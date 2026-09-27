import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const sourcePath = new URL('./exercise-player.component.ts', import.meta.url);
const source = readFileSync(sourcePath, 'utf8');

const validation = source.indexOf("throw new Error('Seviye tespit metni sunucudan alınamadı.");
const guardedInitialization = source.lastIndexOf('try {', validation);
const loadingCleanup = source.indexOf('this.finishLoading();', validation);

assert.ok(validation >= 0, 'assessment reading text validation must remain present');
assert.ok(guardedInitialization >= 0, 'assessment initialization must be guarded');
assert.ok(
  guardedInitialization < validation,
  'missing assessment text must be handled inside the initialization try block',
);
assert.ok(
  loadingCleanup > validation,
  'the loading state must be cleared after assessment initialization fails',
);

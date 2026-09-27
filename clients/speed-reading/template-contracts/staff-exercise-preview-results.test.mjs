import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const component = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.ts', import.meta.url), 'utf8');
const template = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');

test('staff preview shows a local result without claiming a server save', () => {
  assert.match(component, /resultSaveStatus:\s*'idle' \| 'saving' \| 'saved' \| 'failed' \| 'preview'/);
  assert.match(component, /this\.resultSaveStatus = 'preview'/);
  assert.match(template, /Önizleme sonucu.*kaydedilmedi/);
});

test('adaptive-fluency preview can finish without a server session', () => {
  assert.match(component, /handleAdaptiveReadingCompleted\([\s\S]*?sessionId === 'preview-mode'/);
  assert.match(component, /advanceAdaptiveStage\([\s\S]*?sessionId === 'preview-mode'/);
});

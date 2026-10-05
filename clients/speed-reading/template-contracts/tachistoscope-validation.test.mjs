import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const source = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.ts', import.meta.url), 'utf8');
const html = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');

test('tachistoscope actions use the authoritative engine response callback', () => {
  assert.match(source, /engineType === 'text_stream'[\s\S]*?TextStreamEngine\)\.reconcileServerResponse\(action, response\)/);
  assert.match(source, /private shouldTrackReading\(\)[\s\S]*?&& !this\.isTachistoscopeMode\(\)/);
  assert.match(source, /this\.isTachistoscopeMode\(\) && !this\.isAssessmentMode\) this\.refreshTachistoscopeFeedback/);
});

test('tachistoscope pause, exit cancellation and restored paused sessions synchronize with the server', () => {
  assert.match(source, /async togglePause\(\)[\s\S]*?await this\.actionQueue[\s\S]*?resumeSession[\s\S]*?pauseSession/);
  assert.match(source, /async cancelExit\(\)[\s\S]*?await this\.togglePause\(\)/);
  assert.match(source, /async startExercise\(\)[\s\S]*?restoredTachistoscopePaused[\s\S]*?resumeSession/);
});

test('tachistoscope displays stimulus duration and response latency instead of reading speed', () => {
  assert.match(html, /isTachistoscopeMode\(\) \? 'Gösterim süresi' : 'Hız'/);
  assert.match(html, /isTachistoscopeMode\(\) \? 'ms' : 'WPM'/);
  assert.match(html, /Ortalama cevap süresi/);
  assert.match(html, /initialDurationMs[\s\S]*?finalDurationMs/);
});

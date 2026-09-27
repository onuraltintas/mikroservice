import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const html = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');
const scss = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.scss', import.meta.url), 'utf8');
const motionPath = html.split('<!-- Exercise Running - Motion Path (Eye Tracking / Fixation) -->')[1]
  ?.split('<!-- Exercise Running - Text Stream')[0] ?? '';

test('eye tracking and fixation share one compact information strip', () => {
  assert.match(motionPath, /class="game-header motion-path-header"/);
  assert.match(motionPath, /class="game-stats motion-path-stats"[^>]*\*ngIf="!isSaccadeMode\(\)"/);
  assert.match(motionPath, /class="motion-path-actions"/);
  assert.match(scss, /\.motion-path-stats\s*\{[^}]*width:\s*auto/);
});

test('saccade does not render the empty grey statistics bar', () => {
  assert.match(motionPath, /\*ngIf="!isSaccadeMode\(\)"/);
  assert.match(scss, /\.motion-path-stats\s*\{[^}]*background:\s*transparent/);
});

test('exit confirmation renders an icon instead of the warning ligature', () => {
  assert.match(html, /class="material-icons exit-icon">warning<\/span>/);
  assert.doesNotMatch(html, /class="material-icons" class="exit-icon"/);
});

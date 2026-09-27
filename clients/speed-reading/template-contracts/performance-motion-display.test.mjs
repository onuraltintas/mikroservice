import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const performanceTemplate = readFileSync(new URL(
  '../src/app/features/student/dashboard/widgets/performance-analysis-widget.component.html',
  import.meta.url,
), 'utf8');
const playerTemplate = readFileSync(new URL(
  '../src/app/features/student/exercises/universal-player/exercise-player.component.html',
  import.meta.url,
), 'utf8');

assert.match(performanceTemplate, /readingSpeed\.current\s*\|\s*number:'1\.0-0'/);

const motionHeader = playerTemplate.split('<!-- Exercise Running - Motion Path')[1]
  ?.split('<div class="eye-tracking-area">')[0] ?? '';
assert.match(motionHeader, /engineState\.targetCount\s*\|\|\s*0\s*}} hedef/);
assert.doesNotMatch(motionHeader, /<span class="label">Hedefler<\/span>/);

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const templatePath = new URL(
  '../src/app/features/student/exercises/universal-player/exercise-player.component.html',
  import.meta.url,
);

const template = readFileSync(templatePath, 'utf8');
const readingPanel = template.match(
  /engine\.engineType === 'reading_comprehension'[\s\S]*?exercisePhase === 'reading'/,
)?.[0] ?? '';

assert.match(readingPanel, /engine\.engineType === 'free_reading'/);

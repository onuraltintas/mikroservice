import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const templatePath = new URL(
  '../src/app/features/student/exercises/universal-player/exercise-player.component.html',
  import.meta.url,
);
const componentPath = new URL(
  '../src/app/features/student/exercises/universal-player/exercise-player.component.ts',
  import.meta.url,
);

const template = readFileSync(templatePath, 'utf8');
const component = readFileSync(componentPath, 'utf8');
const readingPanel = template.match(
  /engine\.engineType === 'reading_comprehension'[\s\S]*?exercisePhase === 'reading'/,
)?.[0] ?? '';

assert.match(readingPanel, /engine\.engineType === 'free_reading'/);

const comprehensionText = component.match(
  /getComprehensionText\(\): string \{[\s\S]*?\n  \}/,
)?.[0] ?? '';
const comprehensionWordCount = component.match(
  /getComprehensionWordCount\(\): number \{[\s\S]*?\n  \}/,
)?.[0] ?? '';

assert.match(comprehensionText, /engine\?\.engineType === 'free_reading'/);
assert.match(comprehensionWordCount, /engine\?\.engineType === 'free_reading'/);
assert.match(template, /engine\?\.engineType === 'free_reading' && result\?\.details\?\.wpm !== null/);

const visualizationPath = new URL(
  '../src/app/features/student/exercises/universal-player/engines/visualization.engine.ts',
  import.meta.url,
);
const visualizationEngine = readFileSync(visualizationPath, 'utf8');
assert.match(visualizationEngine, /answer: this\.toOptionLetter\(question, answer\)/);

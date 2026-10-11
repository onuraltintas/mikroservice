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
assert.match(template, /isManualReadingEngine\(\) && exercisePhase === 'reading'/);
const manualReadingEngines = component.match(/isManualReadingEngine\(\): boolean \{[\s\S]*?\n  \}/)?.[0] ?? '';
assert.match(manualReadingEngines, /'free_reading'/);

const comprehensionText = component.match(
  /getComprehensionText\(\): string \{[\s\S]*?\n  \}/,
)?.[0] ?? '';
const comprehensionWordCount = component.match(
  /getComprehensionWordCount\(\): number \{[\s\S]*?\n  \}/,
)?.[0] ?? '';

assert.match(comprehensionText, /isManualReadingEngine\(\)/);
assert.match(comprehensionWordCount, /isManualReadingEngine\(\)/);
assert.match(template, /engine\?\.engineType === 'free_reading' && result\?\.details\?\.wpm !== null/);

const visualizationPath = new URL(
  '../src/app/features/student/exercises/universal-player/engines/visualization.engine.ts',
  import.meta.url,
);
const visualizationEngine = readFileSync(visualizationPath, 'utf8');
assert.match(visualizationEngine, /answer: this\.toOptionLetter\(question, answer\)/);

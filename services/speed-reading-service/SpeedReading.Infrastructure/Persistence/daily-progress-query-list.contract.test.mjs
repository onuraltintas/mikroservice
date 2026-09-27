import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const source = readFileSync(new URL('./OwnedSpeedReadingDailyProgress.cs', import.meta.url), 'utf8');
const learningPathSource = readFileSync(new URL('./OwnedSpeedReadingLearningPaths.cs', import.meta.url), 'utf8');
const method = source.split('private async Task<List<DailyExerciseSummary>> BuildExercisesAsync(')[1]
  ?.split('var completedLogs =')[0] ?? '';

test('daily exercise EF queries capture lists, not arrays with span-based Contains', () => {
  assert.match(method, /var exerciseIds = slots\.Select\(item => item\.ExerciseId\)\.ToList\(\);/);
  assert.match(method, /var typeIds = exercises\.Values\.Select\(item => item\.ExerciseTypeId\)\.Distinct\(\)\.ToList\(\);/);
});

test('learning path EF node queries do not capture span-backed arrays', () => {
  assert.doesNotMatch(learningPathSource, /var nodeIds = nodes\.Select\(item => item\.Id\)\.ToArray\(\);/);
  assert.equal((learningPathSource.match(/var nodeIds = nodes\.Select\(item => item\.Id\)\.ToList\(\);/g) ?? []).length, 2);
});

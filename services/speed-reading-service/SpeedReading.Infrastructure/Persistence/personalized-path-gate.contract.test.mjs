import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const backend = readFileSync(new URL('./OwnedSpeedReadingLearningPaths.cs', import.meta.url), 'utf8');
const controller = readFileSync(new URL('../../SpeedReading.API/Controllers/LearningPathsController.cs', import.meta.url), 'utf8');
const service = readFileSync(new URL('../../../../clients/speed-reading/src/app/core/services/learning-path.service.ts', import.meta.url), 'utf8');

test('personalized path requires seven completed program days', () => {
  assert.match(backend, /completedDays >= 7/);
  assert.match(backend, /LearningPath\.FirstWeekIncomplete/);
  assert.match(controller, /personalized\/availability/);
});

test('recommendation batches are short and progress uses its summary endpoint', () => {
  assert.match(backend, /Take\(3\)/);
  assert.match(backend, /Take\(2\)/);
  assert.match(service, /personalized\/progress/);
  assert.doesNotMatch(service, /getPersonalizedLearningPath\(1, 100\)/);
});

test('support recommendations are optional and cannot mutate the main program level', () => {
  assert.match(backend, /decision\.Kind != AdaptiveProgressionDecisionKind\.Support/);
  assert.doesNotMatch(backend, /ApplyAdaptiveLevel/);
  assert.match(backend, /item\.TypeCode == weakTypeCode/);
  assert.match(backend, /supportTextIds\.Contains\(item\.Id\)/);
});

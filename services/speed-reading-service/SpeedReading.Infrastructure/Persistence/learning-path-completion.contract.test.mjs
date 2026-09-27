import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('./OwnedSpeedReadingLearningPaths.cs', import.meta.url), 'utf8');
assert.match(source, /Guid sessionId,/);
assert.match(source, /item\.IsUnlocked/);
assert.match(source, /db\.ExerciseSessionResults/);
assert.match(source, /item\.ContentId/);

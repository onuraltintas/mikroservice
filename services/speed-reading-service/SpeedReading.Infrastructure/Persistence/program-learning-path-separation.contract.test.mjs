import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const learningPaths = readFileSync(new URL('./OwnedSpeedReadingLearningPaths.cs', import.meta.url), 'utf8');
const dailyProgress = readFileSync(new URL('./OwnedSpeedReadingDailyProgress.cs', import.meta.url), 'utf8');

assert.doesNotMatch(learningPaths, /ApplyAdaptiveProgramDifficultyAsync/);
assert.doesNotMatch(dailyProgress, /pattern\.Difficulty\s*,\s*template\.InitialDifficultyLevel \+ progress\.AdaptiveDifficultyOffset/s);

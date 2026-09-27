import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const player = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.ts', import.meta.url), 'utf8');
const service = readFileSync(new URL('../src/app/core/services/learning-path.service.ts', import.meta.url), 'utf8');

assert.match(player, /pathItemId/);
assert.match(player, /completePersonalizedPathItem/);
assert.match(player, /if \(!isAssessmentMode && this\.pathItemId.*?\} else if \(!isAssessmentMode && !isPracticeMode && this\.exercise\?\.id\) \{\s*this\.completeDailyProgress/s);
assert.match(service, /sessionId: string/);

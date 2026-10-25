import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const player = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.ts', import.meta.url), 'utf8');
const service = readFileSync(new URL('../src/app/core/services/learning-path.service.ts', import.meta.url), 'utf8');

assert.match(player, /pathItemId/);
assert.match(player, /completePersonalizedPathItem/);
assert.match(player, /if \(!incomplete && !isAssessmentMode && this\.pathItemId && this\.sessionId\)\s*\{\s*this\.learningPathService\.completePersonalizedPathItem\(this\.pathItemId, this\.sessionId\)/s);
assert.match(player, /else if \(!incomplete && !isAssessmentMode && !isPracticeMode && !this\.reviewItemId && this\.exercise\?\.id\) \{\s*this\.completeDailyProgress/s);
assert.match(service, /sessionId: string/);

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const progress = readFileSync(new URL('../../SpeedReading.Domain/Programs/StudentProgramProgress.cs', import.meta.url), 'utf8');
const daily = readFileSync(new URL('./OwnedSpeedReadingDailyProgress.cs', import.meta.url), 'utf8');
const assignment = readFileSync(new URL('./OwnedSpeedReadingAssessment.cs', import.meta.url), 'utf8');

assert.match(progress, /ScheduleJson/);
assert.match(daily, /program\.Value\.Progress\.ScheduleJson|progress\.ScheduleJson/);
assert.match(assignment, /OwnedSpeedReadingProgramSchedule\.BuildAsync/);
assert.doesNotMatch(daily, /FindCandidatesAsync/);
assert.match(daily, /assignedTotalDays:/);

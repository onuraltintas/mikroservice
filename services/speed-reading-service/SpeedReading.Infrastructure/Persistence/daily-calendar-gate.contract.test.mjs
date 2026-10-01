import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const backend = readFileSync(new URL('./OwnedSpeedReadingDailyProgress.cs', import.meta.url), 'utf8');
const dailyPage = readFileSync(new URL('../../../../clients/speed-reading/src/app/features/student/daily-exercises/daily-exercises.component.ts', import.meta.url), 'utf8');

test('future program days are unavailable for listing and completion', () => {
  assert.match(backend, /GetAvailableDay\(program\.Value\.Progress\.AssignedDate, DateTime\.UtcNow, program\.Value\.Progress\.IsStaffTraining/);
  assert.match(backend, /if \(dayNumber > visibleDay\)\s*return \[\]/);
  assert.match(backend, /DailyProgress\.DayLocked/);
});

test('past days remain available as practice and week boundaries use cumulative days', () => {
  assert.match(backend, /GetActiveOrLatestProgramAsync\(userId, cancellationToken\)/);
  assert.match(dailyPage, /\(\(currentProgress\.currentWeek - 1\) \* 7\) \+ currentProgress\.currentDay/);
  assert.match(dailyPage, /practiceMode: exercise\.isCompleted/);
});

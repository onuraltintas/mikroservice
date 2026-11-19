import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const backend = readFileSync(new URL('./OwnedSpeedReadingDailyProgress.cs', import.meta.url), 'utf8');
const dailyPage = readFileSync(new URL('../../../../clients/speed-reading/src/app/features/student/daily-exercises/daily-exercises.component.ts', import.meta.url), 'utf8');

test('future program days are unavailable for listing and completion', () => {
  assert.match(backend, /GetAvailableDay\(progress\.AssignedDate, now, progress\.IsStaffTraining, progress\.EarlyAdvanceDays\)/);
  // Live test 19.11.2026 (A4/K1): listing, today's plan and completion all use the same gate, which also keeps
  // the next program day closed until the day after the previous one was finished.
  assert.match(backend, /ApplyOneDayPerCalendarDay\(/);
  assert.equal((backend.match(/await AvailableDayAsync\(/g) ?? []).length, 4);
  assert.match(backend, /if \(dayNumber > visibleDay\)\s*return \[\]/);
  assert.match(backend, /DailyProgress\.DayLocked/);
});

test('past days remain available as practice and week boundaries use cumulative days', () => {
  assert.match(backend, /GetActiveOrLatestProgramAsync\(userId, cancellationToken\)/);
  assert.match(dailyPage, /\(\(currentProgress\.currentWeek - 1\) \* 7\) \+ currentProgress\.currentDay/);
  assert.match(dailyPage, /practiceMode: exercise\.isCompleted/);
});

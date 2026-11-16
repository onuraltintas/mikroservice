// Live test 16.11.2026: source checks for the first round of fixes.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

test('#45: the page body is not a second scroll box', () => {
  const styles = read('clients/admin-panel/src/styles.scss');
  const body = styles.slice(styles.indexOf('\nbody {'), styles.indexOf('\nbody,'));
  assert.match(body, /overflow-x: clip;/);
  assert.doesNotMatch(body, /overflow(-[xy])?: (hidden|auto|scroll);/);
});

test('#4/#31: a plan that starts today leaves out the hours that have passed', () => {
  const service = read('services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs');
  assert.match(service, /new WeeklyBlockedTime\(request\.StartDate,0,gone\)/);
  assert.match(service, /notBefore:elapsedToday is \{\} passed\?\(request\.StartDate,passed\):null/);
});

test('#28: started work falls back to another study hour before it is refused', () => {
  const continuation = read('services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingStudyContinuation.cs');
  assert.match(continuation, /Find\(true,true\)\?\?Find\(false,true\)\?\?Find\(true,false\)\?\?Find\(false,false\)/);
  assert.doesNotMatch(continuation, /seçilen saatlere sığmıyor/);
});

test('#35: the rest of a started work is labelled as a continuation', () => {
  const calendar = read('clients/admin-panel/src/app/features/coaching-portal/pages/study-plan-calendar.ts');
  assert.match(calendar, /if\(task\.predecessorTaskId\)return 'Yarım kalanın devamı';/);
});

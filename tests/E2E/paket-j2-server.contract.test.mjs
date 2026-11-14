// Paket J, server part (after the live test of release 92624d7, 14.11.2026).
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = 'services/coaching-service/Coaching.Infrastructure/';

test('a teacher is told when the invited student is already their student', async () => {
  const store = await read(service + 'Profiles/CoachingInvitationStore.cs');
  assert.match(store, /TeacherInviteAlreadyStudent = "Bu öğrenci zaten öğrencinizdir/);
  assert.match(store, /x\.TeacherUserId == actor\s*&& x\.StudentUserId == student\.UserId && x\.IsActive, ct\)\)\s*throw CoachingProfileStore\.Conflict\(TeacherInviteAlreadyStudent\);/);
});

test('the week of the today page is counted as the weekly report counts it', async () => {
  const today = await read(service + 'StudyPlanning/CoachingStudentTodayService.cs');
  assert.match(today, /CoachingStudentStudyReportService\.WeeksAsync\(db, student, weekStart, 1, ct\)/);
  assert.doesNotMatch(today, /x\.RevisionId == planId && x\.SkippedAt == null/);
});

test('a lesson\'s short tasks are grouped in the view only; each stays its own task', async () => {
  const pages = 'clients/admin-panel/src/app/features/coaching-portal/pages/';
  const page = await read(pages + 'student-study-plans.component.html');
  assert.equal(page.match(/@for \(item of dayItems\(/g).length, 2);
  assert.match(page, /<details class="short-run" data-testid="short-run">/);
  const rule = await read(pages + 'plan-short-runs.ts');
  assert.match(rule, /SHORT_TASK_MINUTES = 20;/);
  assert.match(rule, /SHORT_RUN_MINUTES = 60;/);
  // The scheduler's sessions still join outcomes of one topic only.
  const scheduler = await read('services/coaching-service/Coaching.Application/StudyPlanning/WeeklyOutcomeScheduler.cs');
  assert.match(scheduler, /last\.Outcome\.TopicId==slice\.Outcome\.TopicId/);
});

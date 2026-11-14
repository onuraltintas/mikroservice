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

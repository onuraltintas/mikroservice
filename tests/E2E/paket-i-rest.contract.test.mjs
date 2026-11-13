// Paket I3–I6 (live user test 13.11.2026): invitations, screens, admin panel and wording.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const app = 'clients/admin-panel/src/app/';

test('no invented testimonial or third-party portrait on the sign-in and registration pages', async () => {
  for (const page of ['features/auth/login/login.component.html', 'features/auth/register/student/student-register.component.html',
    'features/auth/register/parent/parent-register.component.html', 'features/auth/register/teacher/teacher-register.component.html',
    'features/auth/register/institution/institution-register.component.html']) {
    const html = await read(app + page);
    assert.doesNotMatch(html, /pravatar|Ayşe Yılmaz|Zeynep Yılmaz|Emel Hanım|Mehmet Hoca|Atlas Okulları|%40 arttı|%60 azaldı|Binlerce ders/, page);
    assert.match(html, /data-testid="(register|login)-features"/, page);
  }
});

test('an institution teacher cannot invite a student outside the institution, and the student sees who invites', async () => {
  const store = await read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingInvitationStore.cs');
  assert.match(store, /throw CoachingProfileStore\.Conflict\(TeacherInviteNeedsInstitutionMember\);/);
  assert.match(await read('services/coaching-service/Coaching.API/Controllers/CoachingInvitationsController.cs'), /inviterName = FullName\(/);
  assert.match(await read(app + 'features/coaching-portal/onboarding/student-onboarding.component.html'), /invitation\.inviterName \|\|/);
});

test('admin panel: no broken menu entry, a plan period agrees with its days', async () => {
  assert.doesNotMatch(await read(app + 'features/dashboard/layout/sidebar/sidebar.ts'), /label: 'Kurs Yönetimi'/);
  const plans = await read(app + 'features/dashboard/pages/coaching-subscriptions.ts');
  assert.match(plans, /planPeriodMismatch\(\)/);
  assert.doesNotMatch(plans, /refarans/);
});

test('screens say what happened', async () => {
  assert.match(await read('clients/admin-panel/shared/coaching-notification-target.ts'), /Invitation: 'Davet'/);
  assert.match(await read(app + 'features/coaching-portal/pages/student-exam-results.component.ts'), /"Sonuç belgesindeki puan" boş/);
  const exams = await read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-teacher-exams.component.html');
  assert.match(exams, /data-testid="future-exam-warning"/);
  assert.match(await read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-institution-subscription.component.ts'), /Koltuk bekliyor/);
  assert.doesNotMatch(await read(app + 'features/coaching-portal/growth/student-week-summary.component.ts'), /Bu haftanın değerlendirmesi tamam/);
  assert.match(await read(app + 'features/coaching-portal/pages/coaching-finance.component.html'), /Öğrencilerinizle olan koçluk ücretlerini/);
  assert.doesNotMatch(await read('services/coaching-service/Coaching.Application/Consumers/WelcomeGoal.cs'), /koç aramaya/);
});

test('sessions, the staff portal locale and the other-account warning', async () => {
  const sessions = await read(app + 'features/coaching-portal/pages/coaching-sessions.component.ts');
  assert.match(sessions, /session\.status !== 'Cancelled' && new Date\(session\.startTime\)/);
  assert.match(sessions, /orderedSessions\(\)/);
  const staff = await read('clients/admin-panel/projects/staff-portal/src/app/app.config.ts');
  assert.match(staff, /registerLocaleData\(localeTr\)/);
  assert.match(staff, /provide: LOCALE_ID, useValue: 'tr'/);
  const auth = await read(app + 'core/auth/auth.service.ts');
  assert.match(auth, /new BroadcastChannel\('eduivme-session'\)/);
  // User decision 14.11.2026: the tab warns; it neither signs out nor navigates.
  const layout = await read(app + 'features/coaching-portal/layout/coaching-portal-layout.component.html');
  assert.match(layout, /data-testid="other-account-warning"/);
});

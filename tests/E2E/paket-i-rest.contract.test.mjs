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
  assert.match(await read(app + 'features/coaching-portal/pages/student-exam-results.component.ts'), /puanın yoksa alanı boş bırak/);
  const exams = await read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-teacher-exams.component.html');
  assert.match(exams, /data-testid="future-exam-warning"/);
  assert.match(await read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-institution-subscription.component.ts'), /Koltuk bekliyor/);
  assert.doesNotMatch(await read(app + 'features/coaching-portal/growth/student-week-summary.component.ts'), /Bu haftanın değerlendirmesi tamam/);
  assert.match(await read(app + 'features/coaching-portal/pages/coaching-finance.component.html'), /Öğrencilerle olan koçluk ücretlerini/);
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

test('small screen items: invitations, teacher report, parent page, diploma grade, wizard', async () => {
  const profile = await read('clients/admin-panel/shared/coaching-profile.component.ts');
  assert.match(profile, /data-testid="invitation-notice"/);
  assert.match(profile, /this\.invitationAccepted\.emit\(\);/);
  assert.match(await read(app + 'features/coaching-portal/pages/student-coaching-profile.component.ts'), /\(invitationAccepted\)="teachers\?\.load\(\)"/);
  const staff = 'clients/admin-panel/projects/staff-portal/src/app/features/coaching/';
  const report = await read(staff + 'coaching-teacher-student-detail.component.html');
  assert.match(report, /formatDate\(item\.eventDate, item\.type !== 'Exams'\)/);
  assert.match(report, /data-testid="exam-card-note"/);
  assert.match(await read(staff + 'coaching-teacher-students.component.html'), /student\.institutionId \? 'Kurum öğrencisi' : 'Bağımsız öğrenci'/);
  assert.match(await read(app + 'features/coaching-portal/parent/parent-home.component.ts'), /PENDING_POLL_MS = 20_000/);
  assert.match(await read(app + 'features/coaching-portal/growth/student-goal-summary.component.ts'), /data-testid="diploma-from-profile"/);
  assert.match(await read(app + 'features/coaching-portal/pages/wizard-courses-step.component.html'), /data-testid="choose-suggested-courses"/);
  for (const file of ['coaching-institution-analytics.component.ts', 'coaching-institution-exams.component.ts'])
    assert.doesNotMatch(await read(staff + file), /length: 12 \}|\[1, 2, 3, 4, 5/, file);
});

test('the staff portal warns about another account, an institution has a name, one student has no percentile band', async () => {
  const staff = 'clients/admin-panel/projects/staff-portal/src/app/';
  assert.match(await read(staff + 'auth/staff-auth.service.ts'), /new BroadcastChannel\('eduivme-session'\)/);
  assert.match(await read(staff + 'app.component.html'), /data-testid="other-account-warning"/);
  assert.match(await read('services/identity-service/Identity.API/Controllers/InternalCoachingController.cs'), /name = institution\?\.Name/);
  assert.match(await read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingProfileStore.cs'), /accounts\.InstitutionNameAsync\(id, ct\)/);
  assert.match(await read(app + 'features/coaching-portal/pages/student-teachers.component.ts'), /teacher\.institutionName \+ ' öğretmeni'/);
  assert.match(await read(staff + 'features/coaching/coaching-institution-exams.component.html'), /data\.results\.length > 1 \? 'İlk '/);
});

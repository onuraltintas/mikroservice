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

test('#16: a token rotated a moment ago is refused without ending the session; tabs renew in turn', () => {
  const token = read('services/identity-service/Identity.Domain/Entities/RefreshToken.cs');
  assert.match(token, /ReuseGrace = TimeSpan\.FromSeconds\(30\)/);
  const repository = read('services/identity-service/Identity.Infrastructure/Repositories/Repositories.cs');
  assert.match(repository, /now - rotatedAt <= RefreshToken\.ReuseGrace\)\s*\{\s*return false;/);
  const panel = read('clients/admin-panel/src/app/core/auth/auth.service.ts');
  assert.match(panel, /locks\.request\('eduivme-session-refresh', work\) as Promise<T>/);
  for (const source of [panel, read('clients/speed-reading/src/app/core/services/auth.service.ts')]) assert.match(source, /Auth\.InvalidToken/);
});

test('#41/#44: an institution invitation says what it ends before the student accepts', () => {
  const controller = read('services/coaching-service/Coaching.API/Controllers/CoachingInvitationsController.cs');
  assert.match(controller, /ends = x\.Type == "StudentToInstitution" \? ends : null/);
  assert.match(controller, /x\.InstitutionId == null && x\.IsActive\)\.Select\(x => x\.TeacherUserId\)/);
  const profile = read('clients/admin-panel/shared/coaching-profile.component.ts');
  assert.match(profile, /invite\.ends \? 'Kuruma katıl' : 'Kabul et'/);
  assert.match(profile, /invite\.ends \? 'Kendi öğretmenimle kal' : 'Reddet'/);
  assert.match(read('clients/admin-panel/src/app/features/coaching-portal/onboarding/student-onboarding.component.html'), /data-testid="invitation-ends"/);
});

test('#40: a teacher with independent students cannot join an institution; the screen says why', () => {
  const store = read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingProfileStore.cs');
  assert.match(store, /joining && role == CoachingMemberRole\.Teacher && await IndependentStudentCountAsync\(userId, ct\) is > 0/);
  const profile = read('clients/admin-panel/shared/coaching-profile.component.ts');
  assert.match(profile, /data-testid="invitation-blocked"/);
  assert.match(profile, /\[disabled\]="working\(\) \|\| !!invite\.independentStudents"/);
});

test('#2: the sign-in page loads the security check early and prepares the token while the password is typed', () => {
  const service = read('clients/admin-panel/src/app/core/auth/auth-recaptcha.service.ts');
  assert.match(service, /warmUp\(/);
  assert.match(service, /prepareToken\(action: string/);
  assert.match(service, /this\.prepared = undefined;/);
  const page = read('clients/admin-panel/src/app/features/auth/login/login.component.html');
  assert.match(page, /name="password" \(focus\)="prepareSignIn\(\)" \(input\)="prepareSignIn\(\)"/);
  assert.match(read('clients/admin-panel/src/app/features/auth/login/login.component.ts'), /this\.recaptcha\.warmUp\(\);/);
});

test('#50: Hızlı Okuma hands every answer to the application inside the zone, first in the chain', () => {
  assert.match(read('clients/speed-reading/src/app/app.config.ts'), /withInterceptors\(\[zoneReentryInterceptor, apiResponseInterceptor,/);
  assert.match(read('clients/speed-reading/src/app/core/interceptors/zone-reentry.interceptor.ts'), /zone\.run\(\(\) => observer\.next\(event\)\)/);
});

test('#1: sign-up and the legal page do not show the text version or date', () => {
  for (const path of ['clients/admin-panel/src/app/features/auth/registration-legal-consent.component.ts', 'clients/speed-reading/src/app/features/auth/registration-legal-consent.component.ts']) {
    const source = read(path);
    assert.doesNotMatch(source, /sürüm \{\{ page\.version \}\}/);
    assert.match(source, /version: item\.version/, 'the accepted version is still sent with the registration');
  }
  for (const path of ['clients/admin-panel/src/app/features/public/legal-page.component.ts', 'clients/speed-reading/src/app/features/legal/platform-legal-page.component.ts'])
    assert.doesNotMatch(read(path), /Son güncelleme|Sürüm \{\{/);
  for (const path of ['clients/admin-panel/src/app/features/coaching-cms/components/coaching-newsletter-signup.component.ts',
    'clients/speed-reading/src/app/shared/components/newsletter-widget/newsletter-widget.component.html',
    'clients/speed-reading/src/app/features/public/home/sections/newsletter-section/newsletter-section.html'])
    assert.doesNotMatch(read(path), /onay metni sürüm/);
});

test('#23: an invitation notice and the bell panel lead to where the notice is answered', () => {
  const target = read('clients/admin-panel/shared/coaching-notification-target.ts');
  assert.match(target, /notification\.type === 'Invitation'\)\s*return \{ kind: 'profile'/);
  const panel = read('clients/admin-panel/src/app/features/coaching-portal/layout/coaching-notification-panel.component.ts');
  assert.match(panel, /<a class="text open" \[routerLink\]="link\.path"/);
});

test('#44: a session cancelled with the teacher link says why', () => {
  assert.match(read('shared/EduPlatform.Shared.Contracts/Events/Coaching/CoachingEvents.cs'), /public string\? Reason \{ get; init; \}/);
  assert.match(read('services/notification-service/Notification.Application/Consumers/CoachingNotificationConsumers.cs'), /Neden: bu öğretmenle bağlantın sona erdi\./);
  assert.equal(read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingProfileStore.cs').split('Reason = SessionCancelReasons.TeacherLinkEnded').length - 1, 2);
});

test('second group: teacher-facing plan texts, institution name on invitations, no plan actions for a student without permission', () => {
  const plans = read('clients/admin-panel/src/app/features/coaching-portal/pages/student-study-plans.component.html');
  assert.equal(plans.split("staffStudentId?'Planı yayımla':'Planını yayımla'").length - 1, 3);
  assert.match(plans, /@if \(!planningClosed\(\)\) \{\s*<button type="button" class="secondary" \(click\)="planMenu\.open = false; openWizard\(\)"/);
  assert.equal(plans.split('[readOnly]="readOnly || planningClosed()"').length - 1, 2);
  assert.match(read('services/coaching-service/Coaching.API/Controllers/CoachingInvitationsController.cs'), /institutionName = x\.InstitutionId is \{ \} institution/);
  assert.doesNotMatch(read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-institution-subscription.component.ts'), /Etkin kurum aboneliği bulunamadı/);
});

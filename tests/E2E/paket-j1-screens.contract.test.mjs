// Paket J, first part (user decisions 14.11.2026, after the live test of release 92624d7).
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const client = 'clients/admin-panel/';
const pages = client + 'src/app/features/coaching-portal/pages/';
const staff = client + 'projects/staff-portal/src/app/';

test('the staff portal never revokes a session that is not a staff session', async () => {
  const auth = await read(staff + 'auth/staff-auth.service.ts');
  const restore = auth.slice(auth.indexOf('async restoreSession()'), auth.indexOf('async refreshSession()'));
  assert.doesNotMatch(restore, /logout\(\)/);
  assert.match(restore, /this\.clearSession\(\); this\._notStaffAccount\.set\(true\);/);
  assert.match(await read(staff + 'app.component.html'), /data-testid="not-staff-account"/);
});

test('Kurs Yönetimi is offered only to a page opened for it', async () => {
  const shell = await read(staff + 'app.component.html');
  assert.match(shell, /@if \(courseManagementOffered\) \{\s*@defer \(when !auth\.products\(\)\.some\(hasCourseAccess\)\)/);
  assert.match(shell, /@if \(courseManagementOffered\) \{\s*<button type="button" \[class\.selected\]="selectedProduct === 'course-management'"/);
});

test('a saved week that no longer suits is named and the week by need is offered; nothing changes by itself', async () => {
  const summary = await read(pages + 'wizard-summary-step.component.html');
  assert.match(summary, /data-testid="saved-week-notice"[\s\S]*?\(click\)="replaceWeek\(\)"/);
  assert.doesNotMatch(summary, /slots\(\)\.length && !preview\(\) && !rebuildSuggested/);
  assert.doesNotMatch(summary, /saatler derslerine yetmiyor|şu an <strong>/);
  assert.match(summary, /yetişip yetişmediğini yıllık planda görürsün/);
  const code = await read(pages + 'wizard-summary-step.component.ts');
  assert.match(code, /view\.mode !== 'Fill'/);
  assert.match(code, /canCreate: p\.canCreate/);
  assert.doesNotMatch(code, /“Tekrar” adımından|stillLate/);
});

test('a cancelled session is never on top of the list and has no join link, for the teacher and the student', async () => {
  const teacher = await read(staff + 'features/coaching/coaching-teacher-sessions.component.html');
  assert.match(teacher, /@for \(session of orderedSessions\(\); track session\.id\)/);
  assert.match(teacher, /@if \(session\.meetingLink && session\.status !== 'Cancelled'\)/);
  for (const path of [staff + 'features/coaching/coaching-teacher-sessions.component.ts', pages + 'coaching-sessions.component.ts'])
    assert.match(await read(path), /\.\.\.coming\.filter\(session => session\.status !== 'Cancelled'\), \.\.\.coming\.filter\(session => session\.status === 'Cancelled'\), \.\.\.past/);
});

test('staff screens: grades 5-12, decimals with a comma, the wizard from the report, no student voice', async () => {
  assert.doesNotMatch(await read(staff + 'features/coaching/coaching-teacher-students.component.html'), /grade of \[1, 2, 3, 4/);
  assert.doesNotMatch(await read(staff + 'features/coaching/coaching-institution-workspace.component.html'), /grade of \[1,2,3,4/);
  const report = await read(staff + 'features/coaching/coaching-teacher-student-detail.component.html');
  assert.match(report, /summary\.averageExamPercentage \| number:'1\.0-1'/);
  assert.match(await read(staff + 'features/coaching/coaching-staff-study.component.ts'), /data-testid="report-open-wizard"/);
  const plans = await read(pages + 'student-study-plans.component.html');
  assert.match(plans, /staffStudentId \? 'Öğrenci tek aktif planla ilerler\./);
  assert.match(plans, /staffStudentId \? 'Plana buradan devam et'/);
});

test('an unknown address is a page of its own, not a redirect to /dashboard', async () => {
  const routes = await read(client + 'src/app/app.routes.ts');
  assert.doesNotMatch(routes, /path: '\*\*',\s*redirectTo/);
  assert.match(routes, /path: '\*\*',\s*loadComponent: \(\) => import\('\.\/features\/not-found\/not-found\.component'\)/);
});

test('the register cards promise what the product does', async () => {
  const page = await read(client + 'src/app/features/auth/register/register.component.html');
  assert.doesNotMatch(page, /Ders notlarına ulaş|Sınıfını dijitalleştir|personel yönetimi/);
  assert.match(page, /haftalık çalışma planını kur/);
});

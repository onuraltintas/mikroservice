import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const walk = (dir, filter) => readdirSync(new URL(dir, root)).flatMap((name) => {
  const path = `${dir}/${name}`;
  if (name === 'node_modules' || name === 'bin' || name === 'obj' || name === 'Migrations') return [];
  return statSync(new URL(path, root)).isDirectory() ? walk(path, filter) : (filter(path) ? [path] : []);
});
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management';

// 20.11.2026 live test, A3: codes without a message reached users as "İşlem tamamlanamadı".
test('every error code the course-management API returns has a Turkish message in the panel', () => {
  const sources = walk('services/course-management-service', (path) => path.endsWith('.cs') && !/Tests?\//.test(path))
    .map(read).join('\n');
  const returned = new Set();
  for (const match of sources.matchAll(/(?:code = |Exception\()"([A-Z][A-Za-z]+\.[A-Z][A-Za-z]+)"/g)) returned.add(match[1]);
  const table = read(`${panel}/panel/problem-codes.ts`);
  const missing = [...returned].filter((code) => !code.startsWith('Legacy') && !table.includes(`'${code}'`));
  assert.deepEqual(missing, []);
  assert.match(read(`${panel}/panel/panel-state.ts`), /PROBLEM_TEXTS\[code\]/);
});

// A1: <base href="/"> made "#takvim" resolve to "/#takvim", so a reload opened the home page.
test('the panel writes its section into the address with the page path kept', () => {
  const state = read(`${panel}/panel/panel-state.ts`);
  assert.match(state, /export function hashUrl\(fragment: string\)/);
  assert.match(state, /replaceState\(null, '', hashUrl\(next\)\)/);
  const shell = read(`${panel}/panel/course-panel.component.ts`);
  assert.match(shell, /\[href\]="link\(item\.slug\)"/);
  assert.doesNotMatch(shell, /pushState\(null, '', `#/);
  for (const path of walk(panel, (p) => p.endsWith('.ts') && !p.endsWith('.spec.ts'))) {
    assert.doesNotMatch(read(path), /(?:replaceState|pushState)\(null, '', `#/, path);
  }
});

// A4: the browser's own confirm box froze the page and did not look like the app.
test('no course-management screen opens the browser confirm, alert or prompt box', () => {
  const files = [
    ...walk(panel, (p) => p.endsWith('.ts') && !p.endsWith('.spec.ts')),
    ...walk('clients/course-management/src', (p) => p.endsWith('.ts') && !p.endsWith('.spec.ts'))
  ];
  for (const path of files) assert.doesNotMatch(read(path), /(?:window|globalThis)\.(?:confirm|alert|prompt)\b/, path);
});

// B1–B5: calendar and attendance.
test('the calendar does not offer past hours, widens to early lessons and edits a series', () => {
  const calendar = read(`${panel}/panel/sections/calendar.component.ts`);
  assert.match(calendar, /\[disabled\]="state\.readOnly\(\) \|\| isPastSlot\(day\.iso, hour\)"/);
  assert.match(calendar, /readonly hours = computed\(/);
  assert.match(calendar, /\(click\)="openSeriesEdit\(lesson\)">Seriyi düzenle</);
  assert.match(calendar, /updateLessonSeriesSchedule\(id, series\.id/);
  assert.match(calendar, /problemText\(\{ error: \{ code: 'Lesson\.InvalidTime' \} \}\)/);
});

test('"Dersi tamamla" waits for the end of the lesson and saved homework can be corrected or taken back', () => {
  const attendance = read(`${panel}/panel/sections/attendance.component.ts`);
  assert.match(attendance, /\[disabled\]="busy\(\) \|\| !ended\(current\)/);
  assert.match(attendance, /bitince tamamlayabilirsiniz/);
  assert.match(attendance, /readonly givenHomework = computed/);
  assert.match(attendance, /reviseAssignment\(/);
  assert.match(attendance, /deleteAssignment\(/);
  const api = read('services/course-management-service/CourseManagement.API/AssignmentEndpoints.cs');
  assert.match(api, /MapPut\("\/workspaces\/\{workspaceId:guid\}\/assignments\/\{assignmentId:guid\}"/);
  assert.match(api, /MapDelete\("\/workspaces\/\{workspaceId:guid\}\/assignments\/\{assignmentId:guid\}"/);
  assert.match(api, /code = "Assignment\.InUse"/);
});

// C and D: students and payments.
test('students: a group is offered once, the name opens the card from the keyboard, names sort in Turkish order', () => {
  const students = read(`${panel}/panel/sections/students.component.ts`);
  assert.match(students, /@for \(group of openGroups\(form\.groupIds\)/);
  assert.match(students, /<button type="button" class="row-link"/);
  assert.match(read('services/course-management-service/CourseManagement.API/TurkishCollation.cs'), /"tr-x-icu"/);
  for (const path of ['StudentSearchEndpoints.cs', 'LearningEndpoints.cs'])
    assert.match(read(`services/course-management-service/CourseManagement.API/${path}`), /EF\.Functions\.Collate\(\w+\.DisplayName, TurkishCollation\.Name\)/);
});

test('payments: only the date is shown, a payment can be taken back with a reason, "yaklaşık" only when cents are split', () => {
  const finance = read(`${panel}/panel/sections/finance.component.ts`);
  assert.match(finance, /<td class="num">\{\{ date\(row\.receivedAt\) \}\}<\/td>/);
  assert.match(finance, /\(click\)="openReverse\(row\)">Geri al</);
  assert.match(finance, /reversePayment\(this\.state\.id\(\), row\.id/);
  assert.match(finance, /evenSplit\(form\.amount, form\.count\) \? '' : 'yaklaşık '/);
});

// E and F: reports, settings, brand.
test('reports are Turkish tables for Excel with plain file names', () => {
  const reports = read(`${panel}/course-management-reports.ts`);
  assert.match(reports, /const CSV_SEPARATOR = ';'/);
  assert.doesNotMatch(reports, /'record_type', 'report', 'workspace', 'scope'/);
  assert.match(reports, /headers: \['Öğrenci', 'Durum'\]/);
  const section = read(`${panel}/panel/sections/reports.component.ts`);
  assert.match(section, /courseReportFileName\(table\.title\)/);
  assert.doesNotMatch(section, /-\$\{this\.state\.id\(\)\}`/);
  assert.doesNotMatch(section, /iletişim bilgileriyle/);
});

test('settings: the profile name is shown, maxkampüs names itself, unsubscribe links default to maxkampus.com', () => {
  const settings = read(`${panel}/panel/sections/settings.component.ts`);
  assert.match(settings, /readonly displayName = signal\(''\)/);
  assert.match(settings, /\{\{ state\.brand\(\) \}\} destek ekibi/);
  assert.doesNotMatch(settings, /eduivme aboneliğinizden/);
  assert.match(read('clients/admin-panel/projects/maxkampus/src/app/panel/panel-gate.component.ts'), /site="maxkampus"/);
  assert.match(read('services/course-management-service/CourseManagement.API/CourseEmails.cs'), /PublicBaseUrl \{ get; init; \} = "https:\/\/maxkampus\.com"/);
});

// G, H, I: sign-up, plan page, admin, account switch.
test('sign-up asks the password twice, both forms can show it, and both have a back button', () => {
  const register = read('clients/admin-panel/projects/maxkampus/src/app/account/register.component.ts');
  assert.match(register, /name="passwordAgain"/);
  assert.match(register, /İki şifre aynı değil/);
  assert.match(register, /'Şifreyi gizle' : 'Şifreyi göster'/);
  assert.match(read('clients/admin-panel/projects/maxkampus/src/app/account/login.component.ts'), /'Şifreyi gizle' : 'Şifreyi göster'/);
  assert.match(read('clients/admin-panel/projects/maxkampus/src/app/account/auth-layout.component.ts'), /\(click\)="back\(\)">← Geri</);
});

test('plan page: maxkampüs colours, a tab title that matches, contracts say TL', () => {
  assert.match(read('clients/admin-panel/projects/maxkampus/src/app/panel/plan-subscription.component.ts'), /--bill-accent: var\(--ds-primary\)/);
  assert.match(read('clients/admin-panel/projects/maxkampus/src/app/panel/panel-gate.component.ts'), /this\.active\(\) \? 'Planım ve ödemeler'/);
  assert.match(read('services/billing-service/Billing.Application/SalesDocuments.cs'), /currency == "TRY" \? "TL" : currency/);
  assert.match(read('services/notification-service/Notification.Application/Consumers/BillingNoticeConsumer.cs'), /notice\.Currency == "TRY" \? "TL"/);
});

test('admin: each maxkampüs site page has its own icon and orders show net and VAT', () => {
  const sidebar = read('clients/admin-panel/src/app/features/dashboard/layout/sidebar/sidebar.ts');
  const siteLines = sidebar.split('\n').filter((line) => /site\('Site: /.test(line));
  assert.equal(siteLines.length, 9);
  assert.ok(siteLines.every((line) => /', '<svg/.test(line)), 'every site page names its own icon');
  assert.equal(new Set(siteLines.map((line) => line.match(/ d="([^"]+)"/)[1])).size, 9, 'no two site pages share an icon');
  assert.match(read('clients/admin-panel/src/app/features/billing/billing-orders.ts'), /Net \{\{ row\.order\.amount - row\.order\.vatAmount/);
});

test('maxkampüs says when another account signed in on the same browser', () => {
  const frame = read('clients/admin-panel/projects/maxkampus/src/app/panel/panel-frame.component.ts');
  assert.match(frame, /LAST_ACCOUNT_KEY = 'mk_last_account'/);
  assert.match(frame, /önceki hesabın oturumu kapandı/);
  assert.match(frame, /addEventListener\?\.\('storage'/);
});

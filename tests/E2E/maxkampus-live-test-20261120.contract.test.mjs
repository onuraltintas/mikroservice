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

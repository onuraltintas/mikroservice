import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Fixes from the 20.11.2026 evening live test (claude/maxkampus-live-test-2026-11-20-e.md).
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';
const site = 'clients/admin-panel/projects/maxkampus/src/app';
const admin = 'clients/admin-panel/src/app/features';
const api = 'services/course-management-service/CourseManagement.API/LearningEndpoints.cs';

test('homework belongs to the nearest lesson, and the attendance screen follows the clock', () => {
  const source = read(`${panel}/sections/attendance.component.ts`);
  assert.match(source, /private homeworkLesson\(item: CourseAssignment\)/);
  assert.match(source, /this\.homeworkLesson\(item\)\?\.id === current\.id/);
  assert.match(source, /readonly now = signal\(Date\.now\(\)\)/);
  assert.match(source, /<= this\.now\(\)/);
  assert.match(source, /choose\(id: string\): void \{ this\.lessonId\.set\(id\); this\.notice\.set\(''\)/);
  assert.doesNotMatch(source, /'de bitince/);
});

test('a full student name typed before the search answers is matched again', () => {
  assert.match(read(`${panel}/panel-state.ts`), /export function sameStudentName/);
  for (const file of ['finance', 'calendar']) {
    const source = read(`${panel}/sections/${file}.component.ts`);
    assert.match(source, /sameStudentName\(this\.studentOptions\(\), text\)[\s\S]*searchStudents[\s\S]*sameStudentName\(this\.studentOptions\(\), text\)/);
  }
});

test('CSV import skips students already there and stops only on rows with errors', () => {
  const endpoints = read(api);
  assert.match(endpoints, /existingPeople\.Contains\(PersonKey\(row\.DisplayName, row\.PhoneNumber\)\)/);
  assert.match(endpoints, /if \(preview\.InvalidCount > 0\)\s*return Results\.BadRequest\(new \{ code = "StudentImport\.ResolveRows"/);
  assert.doesNotMatch(endpoints, /InvalidCount > 0 \|\| (?:current)?[Pp]review\.DuplicateCount > 0/);
  const students = read(`${panel}/sections/students.component.ts`);
  assert.match(students, /!!csvPreview\(\)\?\.invalidCount/);
  assert.match(read('services/course-management-service/CourseManagement.Application/StudentCsvImportParser.cs'), /Öğrenci adı boş; ad sütununu doldurun\./);
});

test('the calendar draws time off and working hours, with unique Turkish day names', () => {
  const source = read(`${panel}/sections/calendar.component.ts`);
  assert.match(source, /WEEKDAYS_SHORT = \['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'\]/);
  assert.match(source, /getTeacherTimeOff\(id\)/);
  assert.match(source, /getTeacherAvailability\(id\)/);
  assert.match(source, /timeOffOn\(day\.iso\)/);
  assert.match(source, /closedOn\(\$index\)/);
  assert.match(source, /\[class\.short\]="height\(lesson\) < 44"/);
});

test('owner screens: hours over lessons warn, packages and the no-group case are shown, counts skip cancelled lessons', () => {
  assert.match(read(`${panel}/sections/teachers.component.ts`), /lessonsOutside\(\)/);
  const finance = read(`${panel}/sections/finance.component.ts`);
  assert.match(finance, /Ders paketleri/);
  assert.match(finance, /getFinanceWorkspaceReport\(id\)\)\.packageBalances/);
  assert.match(finance, /inCourseGroup\(form\.studentId, form\.courseId\)/);
  assert.match(read(`${panel}/sections/overview.component.ts`), /item\.status !== 'Cancelled'/);
  assert.match(read(`${panel}/sections/settings.component.ts`), /getElementById\('email-preview'\)/);
  assert.match(read(`${panel}/panel-state.ts`), /replace\(\/\^-\/, '\\u2212'\)/);
});

test('a teacher typing an owner-only address lands on Genel bakış', () => {
  const source = read(`${panel}/course-panel.component.ts`);
  assert.match(source, /private toOverview\(\): void/);
  assert.match(source, /this\.state\.workspace\(\) && !this\.visibleSections\(\)\.some\(item => item\.id === match\.id\)\) this\.toOverview\(\)/);
});

test('public site: TL everywhere, a space before "+ KDV", a visible footer button, the contact answer in view', () => {
  const text = read(`${site}/core/text.ts`);
  assert.match(text, /'TL'/);
  assert.doesNotMatch(text, /style: 'currency'/);
  assert.match(read(`${site}/site/plan-cards.component.ts`), /\{\{ ' ' \+ \(card\.plan\.vatRate != null \? '\+ KDV ' : ''\)/);
  assert.match(read(`${site}/site/site-shell.component.ts`), /\.foot ::ng-deep \.ds-btn--primary \{ background: var\(--ds-accent\)/);
  assert.match(read(`${site}/site/contact.component.ts`), /getElementById\('contact-sent'\)/);
});

test('admin panel: readable danger buttons, notice and coupon wording', () => {
  const styles = read(`${admin}/billing/billing-page.styles.ts`);
  assert.match(styles, /\.actions \.danger \{ color: var\(--ui-danger\)/);
  assert.doesNotMatch(styles, /#b91c1c/i);
  assert.match(read(`${admin}/eduivme-site/eduivme-site-admin.component.html`), /duyurular kurum panelinin ilk sayfasında görünür/);
  const coupons = read(`${admin}/billing/billing-coupons.ts`);
  assert.match(coupons, /validity\(from: string \| null, to: string \| null\)/);
  assert.doesNotMatch(coupons, /: '—' \}\} – \{\{/);
});

// 21.11.2026: the earlier single-page screen ("Önceki ekran") is removed; its tools live in the panel's sections.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';

const root = new URL('../../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');
const feature = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/';

test('the earlier screen and its menu entry are gone', () => {
  assert.equal(existsSync(new URL(feature + 'course-management-workspace.component.ts', root)), false);
  const state = read(feature + 'panel/panel-state.ts');
  assert.doesNotMatch(state, /id: 'previous'/);
  const panel = read(feature + 'panel/course-panel.component.ts');
  assert.doesNotMatch(panel, /staff-course-management-workspace|CourseManagementWorkspaceComponent/);
  assert.match(panel, /slug === 'onceki-ekran'\) this\.toOverview\(\)/, 'a saved old address opens Genel bakış');
});

test('the student statement moved to the student card', () => {
  const students = read(feature + 'panel/sections/students.component.ts');
  assert.match(students, /Ekstreyi indir \(CSV\)/);
  assert.match(students, /Ekstreyi yazdır/);
  assert.match(students, /buildCourseStudentAccountCsv\(/);
  assert.match(students, /buildCourseStudentAccountPrintHtml\(/);
});

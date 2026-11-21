// 21.11.2026 live test of 34297f3: homework files are switched off on the server (no virus scanner yet), but the panel
// showed a "Dosyalar" button that answered "yetkiniz yok"; homework in a one-to-one lesson answered 404.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const root = new URL('../../', import.meta.url);
const read = path => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel/';

test('the server tells the panel which switched features are on', () => {
  const program = read('services/course-management-service/CourseManagement.API/Program.cs');
  assert.match(program, /api\.MapGet\("\/features"[\s\S]*?features\.StudentPortal, features\.AssignmentAttachments/);
});

test('homework file buttons only show when files are switched on', () => {
  const attendance = read(panel + 'sections/attendance.component.ts');
  const buttons = attendance.match(/\(click\)="toggleFiles\(/g) ?? [];
  assert.equal(buttons.length, 2);
  const guarded = attendance.match(/@if \(state\.features\(\)\.assignmentAttachments\) \{[\s\S]{0,300}?\(click\)="toggleFiles\(/g) ?? [];
  assert.equal(guarded.length, 2, 'both file buttons sit behind the switch');
  const state = read(panel + 'panel-state.ts');
  assert.match(state, /assignmentAttachments: false \}\)/, 'off until the server says otherwise');
  assert.match(state, /this\.loadFeatures\(\)/);
});

test('one-to-one students get homework from the owner and their teacher', () => {
  const endpoints = read('services/course-management-service/CourseManagement.API/AssignmentEndpoints.cs');
  assert.match(endpoints, /var oneToOne = owner \|\| student\.OneToOneTeacherUserId == userId;/);
  assert.match(endpoints, /student\.OneToOneTeacherUserId == userId, ct\)\)\s*return true;/, 'they can mark it');
});

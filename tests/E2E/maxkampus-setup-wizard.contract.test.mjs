import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Setup guide (20.11.2026, claude/maxkampus-kurulum-sihirbazi-plan.md).
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';

test('the guide has eight steps and a summary, each one skippable and saved at once', () => {
  const source = read(`${panel}/sections/setup-wizard.component.ts`);
  for (const step of ['profile', 'teachers', 'rooms', 'courses', 'students', 'fees', 'schedule', 'emails', 'summary'])
    assert.match(source, new RegExp(`id: '${step}'`));
  assert.match(source, /Bu adımı atla/);
  assert.match(source, /Daha sonra devam et/);
  assert.match(source, /Kurulumu bitir, panele geç/);
  assert.match(source, /Birebir ders alıyorsa öğretmeni/, 'a student is placed in a group or marked one-to-one with a teacher');
  assert.match(source, /createFeeAgreementWithInstallments/);
  assert.match(source, /previewSeries/, 'clashes are shown before lessons are opened');
  assert.match(source, /item\.conflicts\.map\(code => problemText/, 'clash reasons are written in Turkish, not as codes');
  assert.match(source, /informedConsentConfirmed: this\.consent/);
  assert.doesNotMatch(source, /#[0-9a-fA-F]{3,6}\b/, 'colours come from the design variables');
});

test('Tahsilat and the guide open fee agreements the same way', () => {
  assert.match(read(`${panel}/sections/finance.component.ts`), /createFeeAgreementWithInstallments\(/);
  assert.match(read(`${panel}/fee-agreements.ts`), /export async function createFeeAgreementWithInstallments/);
});

test('the menu is grouped, the guide opens by itself and can be opened again from Ayarlar and Genel bakış', () => {
  const frame = read(`${panel}/course-panel.component.ts`);
  assert.match(frame, /'Kurulum \(' \+ state\.setupProgress\(\)\.done/);
  assert.match(frame, /eduivme_setup_later_/);
  assert.match(frame, /class="menu-group"/);
  assert.match(frame, /visibilitychange/, 'lists reload when the owner comes back to the tab');
  assert.match(read(`${panel}/sections/settings.component.ts`), /Kurulum rehberini aç/);
  assert.match(read(`${panel}/sections/overview.component.ts`), /Kuruluma devam et/);
});

test('everything the guide does can also be done by hand', () => {
  assert.match(read(`${panel}/sections/courses.component.ts`), /updateGroup\(/);
  const students = read(`${panel}/sections/students.component.ts`);
  assert.match(students, /removeFromGroup\(/);
  assert.match(students, /setOneToOneTeacher\(/);
  const teachers = read(`${panel}/sections/teachers.component.ts`);
  assert.match(teachers, /Öğretmenin adı soyadı/);
  assert.match(teachers, /grup öğretmensiz kalır/);
});

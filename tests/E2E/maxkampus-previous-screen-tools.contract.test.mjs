import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// "Önceki ekran"ı kaldırma, steps 1–4 (claude/maxkampus-live-test-2026-11-20-e.md): the four tools only the earlier
// screen had are now in the panel. The screen itself goes after they are tried live.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';

test('makeup credits and homework files sit in Yoklama ve ödev', () => {
  const attendance = read(`${panel}/sections/attendance.component.ts`);
  assert.match(attendance, /<course-makeup-credits \/>/);
  assert.match(attendance, /<course-homework-files \[assignment\]=/);
  const credits = read(`${panel}/sections/makeup-credits.component.ts`);
  assert.match(credits, /approveMakeupCredit\(/);
  assert.match(credits, /createLesson\([\s\S]*?null, credit\.id\);/);
  const files = read(`${panel}/sections/homework-files.component.ts`);
  for (const call of ['uploadAssignmentAttachment(', 'downloadAssignmentAttachment(', 'reviewAssignment(', 'getAssignmentSubmissions('])
    assert.ok(files.includes(call), call);
});

test('the change record is in Ayarlar, in Turkish, without personal data', () => {
  const settings = read(`${panel}/sections/settings.component.ts`);
  assert.match(settings, /\{ id: 'audit', label: 'İşlem kaydı', slug: 'islemler' \}/);
  assert.match(settings, /auditLabel\(entry\.action\)/);
  assert.match(read(`${panel}/audit-labels.ts`), /'Diğer işlem'/);
});

test('moving from another product is in Öğrenciler, for an owner who has one', () => {
  const students = read(`${panel}/sections/students.component.ts`);
  assert.match(students, /Başka üründen aktar/);
  assert.match(students, /legacySources\(this\.auth\.products\(\)\)\.length > 0/);
  const legacy = read(`${panel}/sections/legacy-import.component.ts`);
  for (const call of ['previewLegacyStudents(', 'commitLegacyStudents(', 'previewLegacyInstitution(', 'commitLegacyInstitution(', 'service.invite('])
    assert.ok(legacy.includes(call), call);
});

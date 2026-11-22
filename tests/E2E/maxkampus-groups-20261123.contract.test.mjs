import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Groups end and students move (user decision 23.11.2026): archive and delete a group, its student count, many students into a
// group at once (the list, a CSV file), and a move from one group to another in one step.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';
const api = 'services/course-management-service/CourseManagement.API';

test('a group is archived or deleted without breaking the record', () => {
  const server = read(`${api}/GroupEndpoints.cs`);
  for (const route of ['groups/{groupId:guid}/archive"', 'groups/{groupId:guid}/restore"', 'groups/{groupId:guid}"',
    'groups/{groupId:guid}/students"', 'groups/{groupId:guid}/students/{studentId:guid}/move"'])
    assert.ok(server.includes(route), route);
  assert.match(server, /code = "Group\.InUse"/);
  assert.match(server, /code = "Group\.HasPlannedLessonsOrStudents"/);
  assert.match(read(`${api}/Program.cs`), /api\.MapGroupEndpoints\(\);/);
  assert.match(read('services/course-management-service/CourseManagement.Domain/CourseGroup.cs'), /public DateTimeOffset\? ArchivedAt/);
  assert.match(read('services/course-management-service/CourseManagement.Infrastructure/Migrations/20261123090000_V1GroupArchive.cs'),
    /AddColumn<DateTimeOffset>\(\s*name: "ArchivedAt",\s*table: "CourseGroups"/);
  // Nothing new goes into an archived group.
  for (const file of ['LearningEndpoints.cs', 'AssignmentEndpoints.cs'])
    assert.match(read(`${api}/${file}`), /code = "Group\.Archived"/, file);
});

test('students who join a group are listed in its planned lessons, and leave them when they go', () => {
  const server = read(`${api}/GroupEndpoints.cs`);
  assert.match(server, /internal static async Task<bool> JoinAsync/);
  assert.match(server, /LessonParticipant\.Create/);
  assert.match(read(`${api}/LearningEndpoints.cs`), /GroupEndpoints\.JoinAsync/);
  assert.match(read(`${api}/SetupEndpoints.cs`), /GroupEndpoints\.LeaveAsync/);
});

test('the panel shows the count, archives, deletes, moves and adds many', () => {
  const courses = read(`${panel}/sections/courses.component.ts`);
  assert.match(courses, /studentsText\(group\)/);
  assert.match(courses, /Arşivdeki grupları göster/);
  assert.match(courses, /kalıcı olarak silinsin mi\? Bu geri alınamaz\./);
  const students = read(`${panel}/sections/students.component.ts`);
  assert.match(students, /Başka gruba taşı/);
  assert.match(students, /öğrenci seçildi/);
  assert.match(students, /Eklenen öğrencileri gruba da koy/);
  // Forms pick only active groups; names in reports come from every group.
  const state = read(`${panel}/panel-state.ts`);
  assert.match(state, /readonly groups = computed\(\(\) => this\.allGroups\(\)\.filter\(group => !group\.archivedAt\)\)/);
  assert.match(read(`${panel}/sections/reports.component.ts`), /state\.allGroups\(\)/);
});

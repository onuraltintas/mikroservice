import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Rooms and branches (claude/maxkampus-live-test-2026-11-20-e.md, "Şube ve derslik düzenleme"), 21.11.2026.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';

test('rooms and branches can be corrected, archived and deleted without breaking the record', () => {
  const server = read('services/course-management-service/CourseManagement.API/RoomEndpoints.cs');
  for (const route of ['branches/{branchId:guid}"', 'branches/{branchId:guid}/archive"', 'branches/{branchId:guid}/restore"',
    'rooms/{roomId:guid}"', 'rooms/{roomId:guid}/archive"', 'rooms/{roomId:guid}/restore"'])
    assert.ok(server.includes(route), route);
  assert.match(server, /code = "Room\.InUse"/);
  assert.match(server, /lesson\.ClearRoom\(\)/);
  assert.match(read('services/course-management-service/CourseManagement.API/Program.cs'), /api\.MapRoomEndpoints\(\);/);
  const settings = read(`${panel}/sections/settings.component.ts`);
  assert.match(settings, /kalıcı olarak silinsin mi\? Bu geri alınamaz\./);
  assert.match(settings, /silmek yerine|Bunun yerine arşive alayım mı\?/);
  for (const planner of ['calendar', 'setup-wizard'])
    assert.doesNotMatch(read(`${panel}/sections/${planner}.component.ts`), /@for \(room of state\.rooms\(\)/, `${planner} plans only into active rooms`);
});

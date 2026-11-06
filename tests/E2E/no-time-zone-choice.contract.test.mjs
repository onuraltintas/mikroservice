import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import test from 'node:test';

// E3 #34 (user decision 06.11.2026): no time zone choice anywhere. Student, parent, teacher, institution and admin panels, the
// first-login wizard, the plan wizard and the staff portal (coaching and course management) all use Europe/Istanbul; the server
// ignores a zone a client still sends.
const root = new URL('../../', import.meta.url).pathname;
async function files(dir, pattern, skip = /node_modules|\/bin\/|\/obj\/|\/dist\/|\.angular|Migrations/) {
  const out = [];
  for (const entry of await readdir(join(root, dir), { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (skip.test(path)) continue;
    if (entry.isDirectory()) out.push(...await files(path, pattern, skip));
    else if (pattern.test(entry.name)) out.push(path);
  }
  return out;
}
const read = path => readFile(join(root, path), 'utf8');
const clientFiles = await files('clients/admin-panel', /\.(ts|html)$/).then(list => list.filter(x => !x.endsWith('.spec.ts')));

test('no page offers a time zone', async () => {
  for (const file of clientFiles) {
    const text = await read(file);
    assert.doesNotMatch(text, /Saat dilimi \(IANA\)|id="time-zone"|supportedValuesOf|CoachingTimeZoneService|detectedTimeZone/, file);
    assert.doesNotMatch(text, /name="[A-Za-z]*[Tt]ime[Zz]one[A-Za-z]*"/, `${file} has a time zone field`);
    assert.doesNotMatch(text, /Saat dilimi:/, `${file} shows a time zone`);
  }
});

test('the time zone setting endpoint is gone and the study times are fixed to Istanbul', async () => {
  const controllers = await files('services/coaching-service/Coaching.API/Controllers', /\.cs$/);
  for (const file of controllers) assert.doesNotMatch(await read(file), /"time-zone"/, file);
  const time = await read('services/coaching-service/Coaching.Domain/CoachingTime.cs');
  assert.match(time, /public const string ZoneId = "Europe\/Istanbul";/);
  for (const file of ['services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingStudyAvailabilityService.cs',
    'services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingWeeklyStudyDraftService.cs'])
    assert.doesNotMatch(await read(file), /request\.TimeZoneId|r\.TimeZoneId/, `${file} still reads the client's zone`);
});

test('course management uses Türkiye time for lesson series and teacher hours', async () => {
  const endpoints = await read('services/course-management-service/CourseManagement.API/LearningEndpoints.cs');
  assert.doesNotMatch(endpoints, /FindSystemTimeZoneById\(request\.TimeZoneId/);
  assert.doesNotMatch(endpoints, /InvalidTimeZone/);
  assert.match(endpoints, /CourseManagementTime\.ZoneId/);
});

// E3 review L5, L6: no leftover zone text; an older series in another zone is told it is read as Türkiye time.
test('no leftover time zone text on the pages', async () => {
  const tracking = await read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.component.html');
  assert.doesNotMatch(tracking, /item\.timeZoneId/);
  for (const file of clientFiles) assert.doesNotMatch(await read(file), /Geçerli bir saat dilimi/, file);
  const workspace = await read('clients/admin-panel/projects/staff-portal/src/app/features/course-management/course-management-workspace.component.html');
  assert.match(workspace, /data-testid="series-zone-notice"/);
  assert.match(workspace, /Türkiye saatine göre/);
  const component = await read('clients/admin-panel/projects/staff-portal/src/app/features/course-management/course-management-workspace.component.ts');
  assert.match(component, /turkeyDefaults\(/);
  assert.doesNotMatch(component, /setHours\(/);
});

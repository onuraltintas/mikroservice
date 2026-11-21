import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// "Sonraki tur" items of claude/maxkampus-live-test-2026-11-20-e.md, done on 21.11.2026.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const panel = 'clients/admin-panel/projects/staff-portal/src/app/features/course-management/panel';

test('a short lesson box shows the name first; the time is small and gives way', () => {
  const calendar = read(`${panel}/sections/calendar.component.ts`);
  assert.match(calendar, /<span class="when">\{\{ time\(lesson\.startsAt\) \}\}<\/span>/);
  assert.match(calendar, /\.lesson\.short strong \{ flex: 0 1 auto; min-width: 0; \}/);
  assert.match(calendar, /\[attr\.title\]="height\(lesson\) < 44 \? lessonTitle\(lesson\) : null"/);
});

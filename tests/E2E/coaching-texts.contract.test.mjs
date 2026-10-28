import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

// Roadmap Y6: Coaching screens and messages are Turkish. Broken UTF-8 (mojibake), raw English enum values
// ("OneOnOne", "Scheduled", "Book", "Clean", "Monthly") and raw ISO dates must not reach the user.

const root = fileURLToPath(new URL('../../', import.meta.url));
const skipped = new Set(['bin', 'obj', 'node_modules', 'Migrations', 'dist', '.angular']);

async function files(dir, extensions, filter = () => true) {
  const found = [];
  for (const entry of await readdir(join(root, dir), { withFileTypes: true }).catch(() => [])) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) { if (!skipped.has(entry.name)) found.push(...await files(path, extensions, filter)); }
    else if (extensions.some(ext => entry.name.endsWith(ext)) && !entry.name.includes('.spec.') && filter(entry.name)) found.push(path);
  }
  return found;
}

const coachingScreens = async () => [
  ...await files('clients/admin-panel/src/app/features/coaching-portal', ['.html', '.ts']),
  ...await files('clients/admin-panel/projects/staff-portal/src/app/features/coaching', ['.html', '.ts']),
  ...await files('clients/admin-panel/src/app/features/dashboard/pages', ['.html', '.ts'], name => name.startsWith('coaching')),
];

const lineOf = (text, index) => text.slice(0, index).split('\n').length;

test('Coaching messages contain no broken Turkish characters', async () => {
  const sources = [
    ...await files('services/coaching-service', ['.cs']),
    ...await files('services/notification-service', ['.cs']),
    ...await coachingScreens(),
  ];
  const broken = [];
  for (const path of sources) {
    const text = await readFile(join(root, path), 'utf8');
    for (const match of text.matchAll(/[ÃÅÄ][\u0080-ÿĀ-ſ‘-›]/g))
      broken.push(`${relative(root, join(root, path))}:${lineOf(text, match.index)} ${match[0]}`);
  }
  assert.deepEqual(broken, [], 'mojibake left in Coaching texts');
});

// The last property of an interpolated expression that holds a code value or a date.
const enumFields = /^(status|type|sessionType|source|category|resourceType|billingPeriod|provider|audience|kind)$/;
const dateFields = /(Date|At|^date)$/;

test('Coaching screens show enum values and dates through Turkish labels and date formats', async () => {
  const raw = [];
  for (const path of await coachingScreens()) {
    const text = await readFile(join(root, path), 'utf8');
    for (const match of text.matchAll(/\{\{\s*([^{}|]+?)\s*\}\}/g)) {
      const expression = match[1].trim();
      if (/[()]/.test(expression) || /[?:+]/.test(expression.replace(/\?\./g, '.'))) continue;
      const field = expression.split('.').pop();
      if (enumFields.test(field) || dateFields.test(field)) raw.push(`${path}:${lineOf(text, match.index)} {{ ${expression} }}`);
    }
  }
  assert.deepEqual(raw, [], 'use coachingLabel / a date format instead of printing the raw value');
});

test('Coaching invitations are e-mailed under the Coaching name', async () => {
  const store = await readFile(join(root, 'services/coaching-service/Coaching.Infrastructure/Profiles/CoachingInvitationStore.cs'), 'utf8');
  assert.match(store, /Product\s*=\s*"coaching"/, 'the Coaching invitation event says which product sent it');
});

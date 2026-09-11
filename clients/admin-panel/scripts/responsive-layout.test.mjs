import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const responsiveFormFiles = [
  '../src/app/features/auth/support/support.component.html',
  '../src/app/features/auth/register/teacher/teacher-register.component.html',
  '../src/app/features/auth/register/student/student-register.component.html',
  '../src/app/features/auth/register/parent/parent-register.component.html',
  '../src/app/features/auth/register/institution/institution-register.component.html',
  '../src/app/features/identity/components/create-user-modal/create-user-modal.ts',
  '../src/app/features/identity/components/edit-user-modal/edit-user-modal.ts',
  '../src/app/features/settings/pages/logs/logs.component.ts',
];

test('admin two-column form sections stack on narrow screens', () => {
  for (const file of responsiveFormFiles) {
    const source = readFileSync(new URL(file, import.meta.url), 'utf8');

    assert.match(source, /grid grid-cols-1 sm:grid-cols-2 gap-[456]/, file);
    assert.doesNotMatch(source, /grid grid-cols-2 gap-[456](?:\s|"|')/, file);
  }
});

test('admin header search does not add a desktop offset on narrow screens', () => {
  const header = readFileSync(
    new URL('../src/app/features/dashboard/layout/header/header.html', import.meta.url),
    'utf8',
  );

  assert.match(header, /w-full max-w-lg lg:max-w-md ml-0 sm:ml-8/);
});

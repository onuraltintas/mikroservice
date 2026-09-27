import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = path => readFileSync(new URL(`../src/app/${path}`, import.meta.url), 'utf8');
const profileGuard = read('core/guards/profile-setup.guard.ts');
const assessmentGuard = read('core/guards/assessment.guard.ts');
const routes = read('features/student/student.routes.ts');

test('admin and editor bypass student profile and assessment prerequisites', () => {
  assert.match(profileGuard, /authService\.hasAdminAccess\(\)\s*\|\|\s*authService\.hasRole\('Editor'\)/);
  assert.match(assessmentGuard, /authService\.hasAdminAccess\(\)\s*\|\|\s*authService\.hasRole\('Editor'\)/);
});

test('student onboarding and dashboard routes redirect admin and editor to exercise preview', () => {
  const staffGuard = read('core/guards/staff-onboarding.guard.ts');
  assert.match(staffGuard, /hasAdminAccess\(\)\s*\|\|\s*authService\.hasRole\('Editor'\)/);
  assert.match(staffGuard, /createUrlTree\(\['\/student\/exercises'\]\)/);
  for (const path of ['profile-setup', 'assessment-intro', 'assessment', 'dashboard']) {
    assert.match(routes, new RegExp(`path: '${path}'[\\s\\S]*?canActivate: \\[[^\\]]*staffOnboardingGuard`));
  }
  assert.match(routes, /path: 'daily-exercises'[\s\S]*?assessmentGuard/);
});

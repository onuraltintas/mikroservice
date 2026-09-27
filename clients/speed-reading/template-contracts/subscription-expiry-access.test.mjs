import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const guard = readFileSync(new URL('../src/app/core/guards/subscription.guard.ts', import.meta.url), 'utf8');
const routes = readFileSync(new URL('../src/app/features/student/student.routes.ts', import.meta.url), 'utf8');
const interceptor = readFileSync(new URL('../src/app/core/interceptors/error.interceptor.ts', import.meta.url), 'utf8');

test('subscription lookup failures deny entry instead of allowing it', () => {
  assert.doesNotMatch(guard, /catchError\(\(\) => of\(true\)\)/);
  assert.match(guard, /\/error\/500/);
});

test('direct exercise routes check paid access while initial assessment stays free', () => {
  assert.match(routes, /path: 'exercises\/universal-player\/:exerciseId',[\s\S]*?canActivate: \[authGuard, subscriptionGuard\]/);
  assert.match(routes, /path: 'assessment',[\s\S]*?canActivate: \[authGuard, profileSetupGuard\]/);
  assert.match(guard, /queryParamMap\.get\('assessmentMode'\)/);
  assert.match(guard, /queryParamMap\.get\('assessmentAttemptId'\)/);
});

test('subscription-specific forbidden response leads to renewal page', () => {
  assert.match(interceptor, /SubscriptionRequired/);
  assert.match(interceptor, /\/no-access/);
});

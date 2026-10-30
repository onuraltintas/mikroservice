import test from 'node:test';
import assert from 'node:assert/strict';
import { extractFailures, toAnnotations } from '../../../tools/ci/report-failed-tests.mjs';

// The CI step that repeats failing frontend tests as annotations used to match every "Error:" line, so the logged
// "API Error: HttpErrorResponse" noise of passing tests filled all ten slots and the real failure never showed.
const esc = '\u001b';
const failing = `
stderr | src/app/a.spec.ts > Users > loads
API Error: HttpErrorResponse { status: 400, url: '/api/users' }
API Error: HttpErrorResponse { status: 400, url: '/api/users' }

 ❯  admin-panel  src/app/tmpprobe/probe.spec.ts (2 tests | 2 failed) 113ms
     × logs noise then fails 8ms

${esc}[31m⎯⎯⎯⎯⎯⎯⎯ Failed Tests 2 ⎯⎯⎯⎯⎯⎯⎯${esc}[39m

 FAIL   admin-panel  src/app/tmpprobe/probe.spec.ts > probe suite > logs noise then fails
AssertionError: expected 1 to be 2 // Object.is equality

- Expected
+ Received

- 2
+ 1

 ❯ src/app/tmpprobe/probe.spec.ts:4:15
      2|   it('logs noise then fails', () => {

⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[1/2]⎯

 FAIL   admin-panel  src/app/tmpprobe/probe.spec.ts > probe suite > times out
Error: Test timed out in 100ms.
If this is a long-running test, pass a timeout value as the last argument or configure it globally with "testTimeout".
 ❯ src/app/tmpprobe/probe.spec.ts:6:3

⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[2/2]⎯

 Test Files  1 failed (1)
      Tests  2 failed (2)
`;

const unhandled = `
 Test Files  163 passed (163)
      Tests  993 passed (993)

⎯⎯⎯⎯⎯⎯⎯ Unhandled Errors ⎯⎯⎯⎯⎯⎯⎯

Vitest caught 1 unhandled error during the test run.
This might cause false positive tests. Resolve unhandled errors to make sure your tests are not affected.

⎯⎯⎯⎯ Uncaught Exception ⎯⎯⎯⎯
TypeError: Cannot read properties of null (reading 'nativeElement')
 ❯ CoachingStudentReportComponent.scrollToTop src/app/features/coaching/report.component.ts:88:20
This error originated in "src/app/features/coaching/report.component.spec.ts" test file. It doesn't mean the error was thrown inside the file itself, but while it was running.
`;

test('reports each failing test with its message and location, never the logged API noise', () => {
  const failures = extractFailures(failing);
  assert.equal(failures.length, 2);
  assert.match(failures[0].title, /probe\.spec\.ts > probe suite > logs noise then fails/);
  assert.match(failures[0].body, /AssertionError: expected 1 to be 2/);
  assert.match(failures[0].body, /probe\.spec\.ts:4:15/);
  assert.match(failures[1].title, /times out/);
  assert.match(failures[1].body, /Test timed out in 100ms/);
  assert.ok(failures.every(item => !/API Error/.test(item.title + item.body)));
});

test('reports unhandled errors that fail a run in which every test passed', () => {
  const failures = extractFailures(unhandled);
  assert.equal(failures.length, 1);
  assert.match(failures[0].title, /Unhandled error/);
  assert.match(failures[0].body, /Cannot read properties of null/);
  assert.match(failures[0].body, /report\.component\.spec\.ts/);
});

test('a clean run produces no annotation and annotations are escaped for GitHub', () => {
  assert.deepEqual(extractFailures(' Test Files  163 passed (163)\n      Tests  993 passed (993)\n'), []);
  const [line] = toAnnotations([{ title: 'a > b', body: 'line 1\nline 2 100%' }], 'frontend-unit-tests.log');
  assert.equal(line, '::error title=frontend-unit-tests.log%3A a > b::line 1%0Aline 2 100%25');
});

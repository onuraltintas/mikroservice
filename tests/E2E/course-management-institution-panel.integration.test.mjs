import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHmac, randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// First release (19.11.2026): an institution panel only. Student portal and homework files are off by default;
// the teacher marks homework in class and sees no finance.
const root = fileURLToPath(new URL('../../', import.meta.url));
const secret = 'course-management-institution-panel-test-secret-0123456789012345';
const connectionString = process.env.COURSE_MANAGEMENT_TEST_CONNECTION_STRING;

function token(product, sub, role) {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub, platform_product: product,
    exp: Math.floor(Date.now() / 1000) + 300, 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role });
  const content = `${encode({ alg: 'HS256', typ: 'JWT' })}.${payload}`;
  return `${content}.${createHmac('sha256', secret).update(content).digest('base64url')}`;
}

async function freePort() {
  const reservation = createServer();
  await new Promise(done => reservation.listen(0, '127.0.0.1', done));
  const { port } = reservation.address();
  await new Promise(done => reservation.close(done));
  return port;
}

test('institution panel: switched-off student portal and files, teacher homework marks, owner-only finance', async () => {
  assert.ok(connectionString, 'the disposable PostgreSQL connection must be configured');
  const base = `http://127.0.0.1:${await freePort()}`;
  // No feature switches are set: the defaults are what production gets.
  const child = spawn('dotnet', [resolve(root, 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll')], {
    cwd: root, stdio: process.env.COURSE_MANAGEMENT_TEST_API_LOG === '1' ? 'inherit' : 'ignore', env: { ...process.env,
      ASPNETCORE_URLS: base, ASPNETCORE_ENVIRONMENT: 'Development',
      JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests',
      CourseManagement__OutboxQueryDelaySeconds: '3600', CourseManagement__ReminderPollingSeconds: '3600',
      COURSE_MANAGEMENT_CONNECTION_STRING: connectionString,
      AllowedHosts: 'localhost;127.0.0.1;course-management-service'
    }
  });

  try {
    let ready = false;
    for (let attempt = 0; attempt < 150 && !ready; attempt++) {
      if (child.exitCode !== null) throw new Error(`API exited: ${child.exitCode}`);
      try { ready = (await fetch(`${base}/health/live`)).ok; } catch {}
      if (!ready) await new Promise(done => setTimeout(done, 100));
    }
    assert.equal(ready, true, 'API must start');

    const call = (path, bearer, method = 'GET', body) => fetch(`${base}/api/course-management/v1${path}`, {
      method, headers: { Authorization: `Bearer ${bearer}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      body: body ? JSON.stringify(body) : undefined
    });
    const ownerId = randomUUID();
    const teacherId = randomUUID();
    const outsiderTeacherId = randomUUID();
    const owner = token('course-management', ownerId, 'CourseInstitutionOwner');
    const teacher = token('course-management', teacherId, 'Teacher');
    const outsider = token('course-management', outsiderTeacherId, 'Teacher');
    const workspaceId = randomUUID();
    const ws = `/workspaces/${workspaceId}`;

    assert.equal((await call('/workspaces', owner, 'POST', { id: workspaceId, name: 'TEST Kurs', type: 1 })).status, 201);

    // Student portal is off: its addresses answer like unknown ones and students cannot be invited.
    for (const path of ['/me/courses', '/me/lessons', '/me/attendance', '/me/makeup-credits', '/me/assignments', '/me/finance'])
      assert.equal((await call(path, owner)).status, 404, `${path} is closed without the student portal`);
    const studentInvite = await call(`${ws}/invitations`, owner, 'POST', { id: randomUUID(), targetUserId: randomUUID(), role: 1 });
    assert.equal(studentInvite.status, 400);
    assert.equal((await studentInvite.json()).code, 'Invitation.StudentPortalDisabled');

    // Teachers still join by invitation through the participation endpoints.
    const invitationId = randomUUID();
    assert.equal((await call(`${ws}/invitations`, owner, 'POST', { id: invitationId, targetUserId: teacherId, role: 2 })).status, 201);
    const accepted = await call(`/participation/invitations/${invitationId}/accept`, teacher, 'POST', { displayName: 'TEST Öğretmen' });
    assert.equal(accepted.status, 200, await accepted.clone().text());

    const courseId = randomUUID();
    const groupId = randomUUID();
    const studentA = randomUUID();
    const studentB = randomUUID();
    assert.equal((await call(`${ws}/courses`, owner, 'POST', { id: courseId, name: 'TEST Matematik', subject: 'Matematik' })).status, 201);
    for (const [id, name] of [[studentA, 'TEST Ayşe'], [studentB, 'TEST Mehmet']])
      assert.equal((await call(`${ws}/students`, owner, 'POST', { id, displayName: name, email: null, phoneNumber: null })).status, 201);
    assert.equal((await call(`${ws}/groups`, owner, 'POST', { id: groupId, courseId, teacherUserId: teacherId, name: 'TEST 8-A' })).status, 201);
    for (const id of [studentA, studentB])
      assert.equal((await call(`${ws}/groups/${groupId}/students/${id}`, owner, 'POST')).status, 201);

    // Finance stays with the owner.
    assert.equal((await call(`${ws}/finance/summary`, teacher)).status, 404);
    assert.equal((await call(`${ws}/finance/fee-agreements`, teacher)).status, 404);
    assert.equal((await call(`${ws}/finance/summary`, owner)).status, 200);

    // The teacher gives homework and marks it in class.
    const assignmentId = randomUUID();
    const dueAt = new Date(Date.now() + 2 * 86400000).toISOString();
    const created = await call(`${ws}/assignments`, teacher, 'POST', { id: assignmentId, courseId, groupId, studentId: null,
      title: 'TEST Sayfa 40–45', instructions: null, dueAt });
    assert.equal(created.status, 201, await created.clone().text());
    const completions = `${ws}/assignments/${assignmentId}/completions`;
    const initial = await (await call(completions, teacher)).json();
    assert.deepEqual(initial.map(item => item.completion), ['NotMarked', 'NotMarked']);

    const marked = await call(completions, teacher, 'PUT', { items: [
      { studentId: studentA, completion: 1, note: null },
      { studentId: studentB, completion: 3, note: '  defter evde kalmış ' }
    ] });
    assert.equal(marked.status, 200, await marked.clone().text());
    const byStudent = Object.fromEntries((await marked.json()).map(item => [item.studentId, item]));
    assert.equal(byStudent[studentA].completion, 'Done');
    assert.equal(byStudent[studentB].completion, 'NotDone');
    assert.equal(byStudent[studentB].note, 'defter evde kalmış');
    assert.ok(byStudent[studentB].markedAt);

    assert.equal((await call(completions, owner)).status, 200, 'the owner sees the marks');
    assert.equal((await call(completions, outsider)).status, 404, 'a teacher outside the workspace sees nothing');
    assert.equal((await call(completions, teacher, 'PUT', { items: [{ studentId: randomUUID(), completion: 1 }] })).status, 404,
      'only the assignment’s students can be marked');
    assert.equal((await call(completions, teacher, 'PUT', { items: [{ studentId: studentA, completion: 7 }] })).status, 400);
    assert.equal((await call(completions, teacher, 'PUT', { items: [] })).status, 400);

    // Files are off: no upload or listing addresses.
    assert.equal((await call(`${ws}/assignments/${assignmentId}/attachments`, teacher)).status, 404);
  } finally {
    child.kill();
  }
});

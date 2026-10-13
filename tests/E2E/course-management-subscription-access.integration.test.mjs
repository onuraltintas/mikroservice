import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const secret = 'course-management-subscription-test-secret-only-01234567890123456789';
const connectionString = process.env.COURSE_MANAGEMENT_TEST_CONNECTION_STRING;

function token(product, sub, role = 'Teacher') {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub,
    platform_product: product, exp: Math.floor(Date.now() / 1000) + 60,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role });
  const content = `${encode({ alg: 'HS256', typ: 'JWT' })}.${payload}`;
  return `${content}.${createHmac('sha256', secret).update(content).digest('base64url')}`;
}

test('expired or suspended workspaces remain readable but reject writes until SystemAdmin reactivates them', async () => {
  assert.ok(connectionString, 'the disposable PostgreSQL connection must be configured');
  const reservation = createServer();
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const base = `http://127.0.0.1:${port}`;
  const attachmentRoot = await mkdtemp(join(tmpdir(), 'course-subscription-attachments-'));
  const child = spawn('dotnet', [resolve(root, 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll')], {
    cwd: root, stdio: process.env.COURSE_MANAGEMENT_TEST_API_LOG === '1' ? 'inherit' : 'ignore', env: { ...process.env,
      ASPNETCORE_URLS: base, ASPNETCORE_ENVIRONMENT: 'Development',
      JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests',
      CourseManagement__AssignmentAttachments__RootPath: attachmentRoot,
      CourseManagement__OutboxQueryDelaySeconds: '3600', CourseManagement__ReminderPollingSeconds: '3600',
      COURSE_MANAGEMENT_CONNECTION_STRING: connectionString,
      AllowedHosts: 'localhost;127.0.0.1;course-management-service'
    }
  });

  try {
    let ready = false;
    for (let attempt = 0; attempt < 100; attempt++) {
      if (child.exitCode !== null) throw new Error(`API exited: ${child.exitCode}`);
      try { ready = (await fetch(`${base}/health/live`)).ok; } catch {}
      if (ready) break;
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    assert.equal(ready, true, 'API must start');

    const request = (path, bearer, options = {}) => fetch(`${base}${path}`, {
      ...options, headers: { ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}), ...options.headers }
    });
    const ownerId = 'fc751a9d-541c-4c86-8e14-5bd89e7f8c14';
    const admin = token('coaching', '78db1a68-57b6-4bad-9af7-3623e28920e5', 'SystemAdmin');
    const owner = token('course-management', ownerId);
    const workspaceId = '6ec99707-4892-43ea-b74e-e742c50f76dd';
    const workspacePath = `/api/course-management/v1/workspaces/${workspaceId}`;
    const subscriptionPath = `/api/course-management/v1/admin/workspaces/${workspaceId}/subscription`;
    const start = new Date(Date.now() - 60000).toISOString();
    const end = new Date(Date.now() + 86400000).toISOString();
    const plan = (endsAt = end, accessStatus = 1) => ({ planName: 'Pilot', activeStudentLimit: 10,
      startsAt: start, endsAt, accessStatus });
    const setPlan = value => request(subscriptionPath, admin, { method: 'PUT', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(value) });
    const createCourse = id => request(`${workspacePath}/courses`, owner, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id, name: `Kurs ${id.slice(0, 8)}`, subject: 'Matematik' }) });

    const workspace = await request('/api/course-management/v1/workspaces', owner, { method: 'POST',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: workspaceId, name: 'Abonelik testi', type: 2 }) });
    assert.ok([200, 201].includes(workspace.status), await workspace.clone().text());
    assert.equal((await setPlan(plan())).status, 200);
    assert.equal((await createCourse('b228ae2c-d74c-4f0f-8dd0-1c0b82566e65')).status, 201,
      'active subscription permits workspace writes');

    assert.equal((await setPlan(plan(end, 2))).status, 200);
    const suspendedCourseWrite = await createCourse('e95947d2-a8d0-4aaf-98af-3d0eed721b92');
    assert.equal(suspendedCourseWrite.status, 409, 'suspended subscription blocks workspace writes');
    assert.deepEqual(await suspendedCourseWrite.json(), { code: 'Subscription.ReadOnly' });
    assert.equal((await request(`${workspacePath}/courses`, owner)).status, 200,
      'workspace course records remain readable while suspended');
    assert.equal((await request('/api/course-management/v1/me/profile', owner, { method: 'PUT',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Profilim' }) })).status, 200,
      'personal profile updates remain outside workspace subscription access');
    assert.equal((await setPlan(plan())).status, 200, 'SystemAdmin can reactivate a workspace');
    assert.equal((await createCourse('ee2dde5b-0a60-401f-88b0-6b47b75939c8')).status, 201,
      'reactivation restores workspace writes');

    assert.equal((await setPlan(plan(new Date(Date.now() - 60000).toISOString(), 1))).status, 200);
    const expiredCourseWrite = await createCourse('ea25b290-1557-4322-9a5c-c14a13c9d715');
    assert.equal(expiredCourseWrite.status, 409, 'an active status cannot write after the end date');
    assert.deepEqual(await expiredCourseWrite.json(), { code: 'Subscription.ReadOnly' });
    assert.equal((await setPlan(plan())).status, 200, 'SystemAdmin can reopen an expired workspace');

    const inviteeId = '1f9fd130-2b30-4b05-9488-2d64e61de391';
    const invitationId = '2bad5e92-b05c-4f74-8068-a239e8f53f47';
    assert.equal((await request(`${workspacePath}/invitations`, owner, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id: invitationId, targetUserId: inviteeId, role: 1 }) })).status, 201);
    assert.equal((await setPlan(plan(end, 2))).status, 200);
    const invitee = token('speed-reading', inviteeId, 'Student');
    assert.equal((await request('/api/course-management/v1/participation/invitations', invitee)).status, 200,
      'pending invitations remain readable');
    const invitationAccept = await request(`/api/course-management/v1/participation/invitations/${invitationId}/accept`, invitee, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Davetli öğrenci' })
    });
    assert.equal(invitationAccept.status, 409, 'invitation acceptance cannot add a member while suspended');
    assert.deepEqual(await invitationAccept.json(), { code: 'Subscription.ReadOnly' });
    assert.equal((await setPlan(plan())).status, 200);
    assert.equal((await request(`/api/course-management/v1/participation/invitations/${invitationId}/accept`, invitee, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Davetli öğrenci' })
    })).status, 200, 'invitation acceptance works again after SystemAdmin reactivation');
  } finally {
    if (child.exitCode === null) {
      const exited = new Promise(resolve => child.once('exit', resolve));
      child.kill();
      await exited;
    }
    await rm(attachmentRoot, { recursive: true, force: true });
  }
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const secret = 'course-management-integration-test-secret-only-01234567890123456789';
const subject = 'c6a5b850-2f27-465d-9c84-f2ac830a9990';
const testConnection = process.env.COURSE_MANAGEMENT_TEST_CONNECTION_STRING;

function token(product, sub = subject, signingSecret = secret, role = 'SystemAdmin') {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub,
    platform_product: product, exp: Math.floor(Date.now() / 1000) + 60,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role });
  const content = `${encode({ alg: 'HS256', typ: 'JWT' })}.${payload}`;
  return `${content}.${createHmac('sha256', signingSecret).update(content).digest('base64url')}`;
}

test('live API enforces product and subject boundaries before database access', async () => {
  const reservation = createServer();
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const base = `http://127.0.0.1:${port}`;
  const child = spawn('dotnet', [resolve(root, 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll')], {
    cwd: root, stdio: 'ignore', env: { ...process.env,
      ASPNETCORE_URLS: base, ASPNETCORE_ENVIRONMENT: 'Development',
      JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests',
      COURSE_MANAGEMENT_CONNECTION_STRING: testConnection ?? 'Host=127.0.0.1;Port=1;Database=course_tests;Username=test;Password=test;Timeout=1',
      AllowedHosts: '*'
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
    const path = '/api/course-management/v1/foundation';
    assert.equal((await request(path)).status, 401);
    for (const product of ['coaching', 'speed-reading']) {
      assert.equal((await request(path, token(product))).status, 403, `${product} token must be refused, including admin`);
    }
    for (const sub of ['', 'invalid', '00000000-0000-0000-0000-000000000000']) {
      assert.equal((await request(path, token('course-management', sub))).status, 403);
    }
    assert.equal((await request(path, token('course-management', subject, 'wrong-key'))).status, 401);
    const response = await request(path, token('course-management'));
    assert.equal(response.status, 200);
    assert.equal((await response.json()).product, 'course-management');
    for (const displayName of ['', '   ', 'x'.repeat(201), null]) {
      const result = await request('/api/course-management/v1/me/profile', token('course-management'), {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName })
      });
      assert.equal(result.status, 400, 'invalid name must fail before database access');
    }
    assert.equal((await request('/health/ready')).status, testConnection ? 200 : 503);
    if (testConnection) {
      const profilePath = '/api/course-management/v1/me/profile';
      const putProfile = (sub, displayName) => request(profilePath, token('course-management', sub), {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName })
      });
      assert.equal((await request(profilePath, token('course-management'))).status, 404);
      const created = await putProfile(subject, '  Öğrenci  ');
      assert.equal(created.status, 200);
      assert.deepEqual(await created.json(), { userId: subject, displayName: 'Öğrenci' });
      assert.equal((await putProfile(subject, 'Yeni ad')).status, 200);
      const otherUser = 'ce9bec0b-b8cb-4dc0-95c2-6c2067aad855';
      assert.equal((await request(profilePath, token('course-management', otherUser))).status, 404);
      assert.equal((await putProfile(otherUser, 'Başka kullanıcı')).status, 200);
      assert.equal((await (await request(profilePath, token('course-management'))).json()).displayName, 'Yeni ad');
      const container = process.env.COURSE_MANAGEMENT_TEST_DB_CONTAINER;
      assert.match(container, /^course-foundation-test-[a-f0-9]{32}$/);
      const sql = `INSERT INTO "Workspaces" ("Id", "OwnerUserId", "Name", "Type", "CreatedAt") VALUES
        ('ef9249b5-b54e-4227-9f41-262d6b06992b', '${subject}', 'Kendi kursum', 1, NOW()),
        ('0b4ed4ad-43d0-4711-9da9-ed26578607bf', '${otherUser}', 'Başka kurs', 1, NOW());`;
      const seeded = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], { input: sql, encoding: 'utf8' });
      assert.equal(seeded.status, 0, seeded.stderr);
      const workspaces = await (await request('/api/course-management/v1/me/workspaces', token('course-management'))).json();
      assert.equal(workspaces.length, 1);
      assert.equal(workspaces[0].name, 'Kendi kursum');

      const teacherToken = token('course-management', subject, secret, 'Teacher');
      const courseStudent = token('course-management', otherUser, secret, 'Student');
      const workspaceId = 'f5384382-060c-4cbc-a825-4d61cd509d60';
      const createWorkspace = bearer => request('/api/course-management/v1/workspaces', bearer, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: workspaceId, name: 'Özel ders alanım', type: 2 })
      });
      assert.equal((await createWorkspace(courseStudent)).status, 403);
      assert.equal((await createWorkspace(teacherToken)).status, 201);
      assert.equal((await createWorkspace(teacherToken)).status, 200, 'workspace retries must not duplicate');
      assert.equal((await createWorkspace(token('coaching', subject, secret, 'Teacher'))).status, 403);
      const invitationId = 'ba5a0d8d-15d3-4212-ad98-1b10d32b110f';
      const invitePath = `/api/course-management/v1/workspaces/${workspaceId}/invitations`;
      const createInvite = (bearer, id = invitationId, role = 1) => request(invitePath, bearer, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, targetUserId: otherUser, role })
      });
      assert.equal((await createInvite(courseStudent)).status, 404);
      assert.equal((await createInvite(teacherToken, invitationId, 3)).status, 400, 'invitations cannot grant owner');
      assert.equal((await createInvite(teacherToken)).status, 201);
      assert.equal((await createInvite(teacherToken)).status, 200);
      const oldStudent = token('speed-reading', otherUser, secret, 'Student');
      const pending = await (await request('/api/course-management/v1/participation/invitations', oldStudent)).json();
      assert.equal(pending.length, 1);
      assert.equal(pending[0].id, invitationId);
      const acceptPath = `/api/course-management/v1/participation/invitations/${invitationId}/accept`;
      const accept = bearer => request(acceptPath, bearer, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Davetli öğrenci' }) });
      assert.equal((await accept(teacherToken)).status, 404, 'only the named recipient can accept');
      const accepts = await Promise.all([accept(oldStudent), accept(oldStudent)]);
      for (const accepted of accepts) assert.ok([200, 409].includes(accepted.status));
      assert.equal((await accept(oldStudent)).status, 200);
      assert.equal((await request('/api/course-management/v1/me/memberships', oldStudent)).status, 403);
      const memberships = await (await request('/api/course-management/v1/me/memberships', courseStudent)).json();
      assert.equal(memberships.length, 1);
      assert.equal(memberships[0].role, 'Student');
      assert.equal((await request(`/api/course-management/v1/workspaces/${workspaceId}/members`, courseStudent)).status, 404);
      const revokePath = `/api/course-management/v1/workspaces/${workspaceId}/members/${otherUser}/revoke`;
      assert.equal((await request(revokePath, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await request(revokePath, teacherToken, { method: 'POST' })).status, 204);
      assert.deepEqual(await (await request('/api/course-management/v1/me/memberships', courseStudent)).json(), []);
      assert.equal((await accept(oldStudent)).status, 409, 'accepted invite must not restore revoked access');
      assert.equal((await request(`/api/course-management/v1/workspaces/${workspaceId}/members/${subject}/revoke`, teacherToken, { method: 'POST' })).status, 409);
      const cancelledId = 'a29bc0c7-1b4e-4974-9668-39a6753d20c6';
      assert.equal((await createInvite(teacherToken, cancelledId)).status, 201);
      assert.equal((await request(`${invitePath}/${cancelledId}/cancel`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await request(`/api/course-management/v1/participation/invitations/${cancelledId}/accept`, oldStudent, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Öğrenci' })
      })).status, 409);
      const audit = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, teacherToken)).json();
      assert.equal(audit.filter(entry => entry.action === 'Workspace.Created').length, 1);
      assert.equal(audit.filter(entry => entry.action === 'Invitation.Accepted').length, 1);
      assert.equal(audit.filter(entry => entry.action === 'Membership.Revoked').length, 1);
    }
  } finally {
    if (child.exitCode === null) {
      const exited = new Promise(resolve => child.once('exit', resolve));
      child.kill();
      await exited;
    }
  }
});

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

function token(product, sub = subject, signingSecret = secret) {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub,
    platform_product: product, exp: Math.floor(Date.now() / 1000) + 60,
    role: 'SystemAdmin' });
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
    }
  } finally {
    if (child.exitCode === null) {
      const exited = new Promise(resolve => child.once('exit', resolve));
      child.kill();
      await exited;
    }
  }
});

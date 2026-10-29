import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { mkdir, mkdtemp, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { spawn, spawnSync } from 'node:child_process';
import { request as httpRequest } from 'node:http';
import { createServer } from 'node:net';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const secret = 'course-management-integration-test-secret-only-01234567890123456789';
const subject = 'c6a5b850-2f27-465d-9c84-f2ac830a9990';
const testConnection = process.env.COURSE_MANAGEMENT_TEST_CONNECTION_STRING;
const testContainer = process.env.COURSE_MANAGEMENT_TEST_DB_CONTAINER;
const eicarSignature = 'X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*';

function utcDayOffset(days) {
  const date = new Date();
  date.setUTCHours(0, 0, 0, 0);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

async function countFiles(directory) {
  let count = 0;
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    count += entry.isDirectory() ? await countFiles(path) : Number(entry.isFile());
  }
  return count;
}

function token(product, sub = subject, signingSecret = secret, role = 'SystemAdmin', email = null) {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub,
    ...(email ? { email } : {}),
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
  const configuredAttachmentRoot = process.env.COURSE_MANAGEMENT_TEST_ATTACHMENT_ROOT;
  const attachmentRoot = configuredAttachmentRoot
    ? resolve(configuredAttachmentRoot)
    : await mkdtemp(join(tmpdir(), 'course-management-attachments-'));
  const preserveAttachmentRoot = process.env.COURSE_MANAGEMENT_TEST_PRESERVE_ATTACHMENTS === '1';
  const externalClamHost = process.env.COURSE_MANAGEMENT_TEST_CLAMAV_HOST;
  const externalClamPort = Number(process.env.COURSE_MANAGEMENT_TEST_CLAMAV_PORT);
  const useExternalClam = Boolean(externalClamHost && Number.isInteger(externalClamPort) && externalClamPort > 0);
  let cancellationScanObserved = false;
  let signalCancellationScan;
  const cancellationScanStarted = new Promise(resolve => { signalCancellationScan = resolve; });
  const clamServer = useExternalClam ? null : createServer(socket => {
    let buffer = Buffer.alloc(0);
    let commandRead = false;
    let complete = false;
    const scannedChunks = [];
    socket.on('data', chunk => {
      if (complete) return;
      buffer = Buffer.concat([buffer, chunk]);
      if (!commandRead) {
        const commandEnd = buffer.indexOf(0);
        if (commandEnd < 0) return;
        buffer = buffer.subarray(commandEnd + 1);
        commandRead = true;
      }
      while (buffer.length >= 4) {
        const length = buffer.readUInt32BE(0);
        if (length === 0) {
          complete = true;
          const scannedContent = Buffer.concat(scannedChunks);
          const infected = scannedContent.includes(Buffer.from(eicarSignature));
          const scannerFailure = scannedContent.includes(Buffer.from('SIMULATED-SCAN-ERROR'));
          const scannerCancellation = scannedContent.includes(Buffer.from('SIMULATED-SCAN-CANCEL'));
          if (scannerCancellation) {
            cancellationScanObserved = true;
            signalCancellationScan();
            return;
          }
          const response = infected ? 'stream: Eicar-Test-Signature FOUND\0'
            : scannerFailure ? 'stream: ERROR\0' : 'stream: OK\0';
          socket.end(Buffer.from(response));
          return;
        }
        if (buffer.length < length + 4) return;
        scannedChunks.push(buffer.subarray(4, 4 + length));
        buffer = buffer.subarray(4 + length);
      }
    });
  });
  if (clamServer) await new Promise(resolve => clamServer.listen(0, '127.0.0.1', resolve));
  const clamHost = useExternalClam ? externalClamHost : '127.0.0.1';
  const clamPort = useExternalClam ? externalClamPort : clamServer.address().port;
  await mkdir(attachmentRoot, { recursive: true });
  const child = spawn('dotnet', [resolve(root, 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll')], {
    cwd: root, stdio: process.env.COURSE_MANAGEMENT_TEST_API_LOG === '1' ? 'inherit' : 'ignore', env: { ...process.env,
      ASPNETCORE_URLS: base, ASPNETCORE_ENVIRONMENT: 'Development',
      JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests',
      CourseManagement__AssignmentAttachments__RootPath: attachmentRoot,
      CourseManagement__AssignmentAttachments__ScannerProvider: 'ClamAv',
      CourseManagement__AssignmentAttachments__ClamAvHost: clamHost,
      CourseManagement__AssignmentAttachments__ClamAvPort: String(clamPort),
      CourseManagement__OutboxQueryDelaySeconds: '3600',
      CourseManagement__ReminderPollingSeconds: '1',
      COURSE_MANAGEMENT_CONNECTION_STRING: testConnection ?? 'Host=127.0.0.1;Port=1;Database=course_tests;Username=test;Password=test;Timeout=1',
      AllowedHosts: 'localhost;127.0.0.1;course-management-service'
    }
  });
  let gatewayChild = null;
  try {
    let ready = false;
    for (let attempt = 0; attempt < 100; attempt++) {
      if (child.exitCode !== null) throw new Error(`API exited: ${child.exitCode}`);
      try { ready = (await fetch(`${base}/health/live`)).ok; } catch {}
      if (ready) break;
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    assert.equal(ready, true, 'API must start');
    const outboxAudit = spawnSync('docker', ['exec', testContainer, 'psql', '-U', 'course_test', '-d', 'course_test', '-v', 'ON_ERROR_STOP=1', '-c',
      'CREATE TABLE "CourseOutboxTestAudit" ("MessageType" text NOT NULL); CREATE FUNCTION capture_course_outbox_test_message() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN INSERT INTO "CourseOutboxTestAudit" ("MessageType") VALUES (NEW."MessageType"); RETURN NEW; END $$; CREATE TRIGGER capture_course_outbox_test_message AFTER INSERT ON "OutboxMessage" FOR EACH ROW EXECUTE FUNCTION capture_course_outbox_test_message();'], { encoding: 'utf8' });
    assert.equal(outboxAudit.status, 0, outboxAudit.stderr);
    const request = (path, bearer, options = {}) => fetch(`${base}${path}`, {
      ...options, headers: { ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}), ...options.headers }
    });
    const requestRawHttp = (address, path, headers = {}) => new Promise((resolve, reject) => {
      const hostRequest = httpRequest(new URL(path, address), { headers }, response => {
        let body = '';
        response.setEncoding('utf8');
        response.on('data', chunk => { body += chunk; });
        response.resume();
        response.on('end', () => resolve({ status: response.statusCode, body }));
      });
      hostRequest.on('error', reject);
      hostRequest.end();
    });
    assert.equal((await requestRawHttp(base, '/health/live', { host: 'course-management-service' })).status, 200,
      'the production Gateway destination host is allowed');
    assert.equal((await requestRawHttp(base, '/health/live', { host: 'untrusted.invalid' })).status, 400,
      'unconfigured hosts are rejected before API handling');
    const path = '/api/course-management/v1/foundation';
    const adminOverviewPath = '/api/course-management/v1/admin/overview';
    const adminSubscriptionsPath = '/api/course-management/v1/admin/subscriptions';
    const adminSubscriptionAuditPath = '/api/course-management/v1/admin/subscription-audit';
    const adminWorkspaceSubscriptionPath = workspaceId => `/api/course-management/v1/admin/workspaces/${workspaceId}/subscription`;
    const mySubscriptionsPath = '/api/course-management/v1/me/subscriptions';
    assert.equal((await request(path)).status, 401);
    assert.equal((await request(adminOverviewPath)).status, 401, 'admin overview requires authentication');
    assert.equal((await request(adminSubscriptionsPath)).status, 401, 'subscription administration requires authentication');
    assert.equal((await request(adminSubscriptionAuditPath)).status, 401, 'subscription audit requires authentication');
    assert.equal((await request(mySubscriptionsPath)).status, 401, 'owner subscription view requires authentication');
    assert.equal((await request(adminOverviewPath, token('coaching', subject, secret, 'Teacher'))).status, 403,
      'a non-admin coaching user cannot read global course operations');
    assert.equal((await request(adminSubscriptionsPath, token('coaching', subject, secret, 'Teacher'))).status, 403,
      'a non-admin cannot list course subscriptions');
    assert.equal((await request(adminSubscriptionAuditPath, token('coaching', subject, secret, 'Teacher'))).status, 403,
      'a non-admin cannot read global subscription audit');
    assert.equal((await request(adminWorkspaceSubscriptionPath('00000000-0000-0000-0000-000000000001'),
      token('course-management', subject, secret, 'Teacher'), { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({}) })).status, 403,
      'a workspace teacher cannot assign platform subscriptions');
    assert.equal((await request(adminOverviewPath, token('speed-reading', subject, secret, 'SystemAdmin'))).status, 403,
      'admin access remains scoped to the existing admin session and course product');
    assert.equal((await request(adminSubscriptionsPath, token('speed-reading', subject, secret, 'SystemAdmin'))).status, 403,
      'subscription administration remains scoped to the course product');
    assert.equal((await request(adminSubscriptionAuditPath, token('speed-reading', subject, secret, 'SystemAdmin'))).status, 403,
      'subscription audit remains scoped to the existing admin session and course product');
    assert.equal((await request(`${adminSubscriptionAuditPath}?skip=-1`, token('coaching'))).status, 400,
      'negative subscription audit page offsets are rejected');
    assert.equal((await request(`${adminSubscriptionAuditPath}?take=101`, token('coaching'))).status, 400,
      'subscription audit page size is bounded');
    const invalidSubscription = await request(adminWorkspaceSubscriptionPath('00000000-0000-0000-0000-000000000001'), token('coaching'), {
      method: 'PUT', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ planName: ' ', activeStudentLimit: 0, startsAt: null, endsAt: null, accessStatus: 9 })
    });
    assert.equal(invalidSubscription.status, 400, 'invalid subscription input is rejected before database access');
    assert.equal((await request(`${adminSubscriptionsPath}?skip=-1`, token('coaching'))).status, 400,
      'negative subscription page offsets are rejected');
    assert.equal((await request(`${adminSubscriptionsPath}?take=101`, token('coaching'))).status, 400,
      'subscription page size is bounded');
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
    const missingDueDate = await request('/api/course-management/v1/workspaces/0b4ed4ad-43d0-4711-9da9-ed26578607bf/assignments',
      token('course-management'), { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: '1125dfd0-8aed-4ded-a76a-86a929bed69c', courseId: 'a6e6651d-3d99-48a1-af8a-b774f4bf3225',
          groupId: '25e175a6-79ef-41da-b575-39ff7cc649cb', studentId: null, title: 'Tarihsiz ödev', instructions: '', dueAt: null }) });
    assert.equal(missingDueDate.status, 400, 'assignment due date must be validated before database access');
    assert.equal((await request('/health/ready')).status, testConnection ? 200 : 503);
    if (testConnection) {
      const gatewayReservation = createServer();
      await new Promise(resolve => gatewayReservation.listen(0, '127.0.0.1', resolve));
      const gatewayPort = gatewayReservation.address().port;
      await new Promise(resolve => gatewayReservation.close(resolve));
      const gatewayBase = `http://127.0.0.1:${gatewayPort}`;
      gatewayChild = spawn('dotnet', [resolve(root, 'services/api-gateway/bin/Debug/net10.0/EduPlatform.Gateway.dll')], {
        cwd: root, stdio: process.env.COURSE_MANAGEMENT_TEST_GATEWAY_LOG === '1' ? 'inherit' : 'ignore', env: { ...process.env,
          ASPNETCORE_CONTENTROOT: resolve(root, 'services/api-gateway'),
          ASPNETCORE_URLS: gatewayBase, ASPNETCORE_ENVIRONMENT: 'Development',
          JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests', JWT_KEY_ID: 'course-tests',
          REDIS_HOST: '127.0.0.1', REDIS_PORT: '1',
          // Course Management routes load only when the product is enabled (off in production).
          CourseManagement__Enabled: 'true',
          'ReverseProxy__Clusters__course-management-cluster__Destinations__destination1__Address': base
        }
      });
      let gatewayReady = false;
      for (let attempt = 0; attempt < 100; attempt++) {
        if (gatewayChild.exitCode !== null) throw new Error(`Gateway exited: ${gatewayChild.exitCode}`);
        try { gatewayReady = (await fetch(`${gatewayBase}/health`)).ok; } catch {}
        if (gatewayReady) break;
        await new Promise(resolve => setTimeout(resolve, 100));
      }
      assert.equal(gatewayReady, true, 'Gateway must start for the Course Management route smoke');
      const gatewayResponse = await requestRawHttp(gatewayBase, path, {
        host: 'untrusted.invalid', authorization: `Bearer ${token('course-management')}`
      });
      assert.equal(gatewayResponse.status, 200, gatewayResponse.body);
      assert.equal(JSON.parse(gatewayResponse.body).product, 'course-management',
        'YARP routes the course request and forwards the configured destination Host, not the untrusted client Host');

      const initialAdminOverviewResponse = await request(adminOverviewPath, token('coaching'));
      assert.equal(initialAdminOverviewResponse.status, 200, await initialAdminOverviewResponse.clone().text());
      const initialAdminOverview = await initialAdminOverviewResponse.json();
      assert.deepEqual(initialAdminOverview, {
        workspaceCount: 0,
        institutionWorkspaceCount: 0,
        independentTeacherWorkspaceCount: 0,
        activeMembershipCount: 0,
        activeStudentCount: 0,
        courseCount: 0,
        scheduledLessonCount: 0,
        pendingInvitationCount: 0
      });
      assert.equal(Object.hasOwn(initialAdminOverview, 'ownerUserId'), false, 'aggregate view must not expose personal data');

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
      const queryCourseDb = sql => spawnSync('docker', ['exec', container, 'psql', '-U', 'course_test', '-d', 'course_test',
        '-v', 'ON_ERROR_STOP=1', '-tAc', sql], { encoding: 'utf8' });
      const readReminders = relatedEntityId => {
        const result = queryCourseDb(`SELECT COALESCE(json_agg(json_build_object('type', "Type", 'status', "Status", 'recipientUserId', "RecipientUserId", 'targetAt', "TargetAt", 'notifyAt', "NotifyAt")), '[]'::json)::text FROM "CourseManagementReminders" WHERE "RelatedEntityId" = '${relatedEntityId}';`);
        assert.equal(result.status, 0, result.stderr);
        return JSON.parse(result.stdout.trim());
      };
      const sql = `INSERT INTO "Workspaces" ("Id", "OwnerUserId", "Name", "Type", "CreatedAt") VALUES
        ('ef9249b5-b54e-4227-9f41-262d6b06992b', '${subject}', 'Kendi kursum', 1, NOW()),
        ('0b4ed4ad-43d0-4711-9da9-ed26578607bf', '${otherUser}', 'Başka kurs', 1, NOW());`;
      const seeded = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], { input: sql, encoding: 'utf8' });
      assert.equal(seeded.status, 0, seeded.stderr);
      const workspaces = await (await request('/api/course-management/v1/me/workspaces', token('course-management'))).json();
      assert.equal(workspaces.length, 1);
      assert.equal(workspaces[0].name, 'Kendi kursum');
      const seededAdminOverview = await (await request(adminOverviewPath, token('coaching'))).json();
      assert.equal(seededAdminOverview.workspaceCount, 2);
      assert.equal(seededAdminOverview.institutionWorkspaceCount, 2);
      assert.equal(seededAdminOverview.activeMembershipCount, 0);
      assert.equal(Object.hasOwn(seededAdminOverview, 'name'), false, 'global overview must not list workspace names');

      const teacherToken = token('course-management', subject, secret, 'Teacher');
      const institutionWorkspaceId = 'd186b940-2198-4b7c-8c1b-05c04ce1981c';
      const institutionOwnerId = 'e43ad254-feba-457d-b089-29eadc4e1df4';
      const institutionOwner = token('course-management', institutionOwnerId, secret, 'Owner');
      const otherTeacherId = 'c6e08d49-b4f3-4d3a-9dd3-c806e190aa93';
      const seedInstitutionWorkspace = queryCourseDb(`INSERT INTO "Workspaces" ("Id", "OwnerUserId", "Name", "Type", "CreatedAt")
        VALUES ('${institutionWorkspaceId}', '${institutionOwnerId}', 'Yetki testi kurumu', 1, NOW());`);
      assert.equal(seedInstitutionWorkspace.status, 0, seedInstitutionWorkspace.stderr);
      const legacyInstitutionImportPath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/imports/legacy-institution`;
      const legacyInstitutionImport = {
        sourceSystem: 'coaching', sourceInstitutionId: '949a185a-3d63-41d7-9e40-b26e5dcdd02e',
        selectedFields: ['name', 'type'], name: '  Örnek dershane  ', institutionCategory: 'PrivateCourse'
      };
      const legacyInstitutionPreviewResponse = await request(`${legacyInstitutionImportPath}/preview`, institutionOwner, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(legacyInstitutionImport)
      });
      assert.equal(legacyInstitutionPreviewResponse.status, 200, await legacyInstitutionPreviewResponse.clone().text());
      const legacyInstitutionPreview = await legacyInstitutionPreviewResponse.json();
      assert.equal(legacyInstitutionPreview.workspaceName, 'Yetki testi kurumu');
      assert.equal(legacyInstitutionPreview.targetName, 'Örnek dershane');
      assert.equal(legacyInstitutionPreview.targetInstitutionCategory, 'PrivateCourse');
      const legacyInstitutionCommit = {
        ...legacyInstitutionImport,
        expectedWorkspaceName: legacyInstitutionPreview.workspaceName,
        expectedWorkspaceCategory: legacyInstitutionPreview.workspaceInstitutionCategory,
        previewId: legacyInstitutionPreview.previewId
      };
      assert.equal((await request(`${legacyInstitutionImportPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(legacyInstitutionImport) })).status, 404,
        'only workspace owners can preview institution information imports');
      assert.equal((await request(legacyInstitutionImportPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(legacyInstitutionCommit) })).status, 404,
        'only workspace owners can commit institution information imports');
      assert.equal((await request(`${legacyInstitutionImportPath}/preview`, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...legacyInstitutionImport, sourceSystem: 'unknown' }) })).status, 400,
        'institution imports accept only known legacy products');
      const legacyInstitutionApplied = await request(legacyInstitutionImportPath, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(legacyInstitutionCommit) });
      assert.equal(legacyInstitutionApplied.status, 200, await legacyInstitutionApplied.clone().text());
      assert.deepEqual(await legacyInstitutionApplied.json(), {
        previewId: legacyInstitutionPreview.previewId, name: 'Örnek dershane', institutionCategory: 'PrivateCourse', alreadyApplied: false
      });
      const repeatedInstitutionImport = await request(legacyInstitutionImportPath, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(legacyInstitutionCommit) });
      assert.deepEqual(await repeatedInstitutionImport.json(), {
        previewId: legacyInstitutionPreview.previewId, name: 'Örnek dershane', institutionCategory: 'PrivateCourse', alreadyApplied: true
      }, 'repeating a previewed metadata import is idempotent');
      assert.equal((await request(legacyInstitutionImportPath, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...legacyInstitutionCommit, name: 'Farklı kurum' }) })).status, 409,
        'import data cannot change after it is previewed');
      const nameOnlyImport = { ...legacyInstitutionImport, selectedFields: ['name'], name: 'Yeni kurum adı', institutionCategory: null };
      const nameOnlyPreviewResponse = await request(`${legacyInstitutionImportPath}/preview`, institutionOwner, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(nameOnlyImport)
      });
      assert.equal(nameOnlyPreviewResponse.status, 200);
      const nameOnlyPreview = await nameOnlyPreviewResponse.json();
      assert.equal(nameOnlyPreview.targetInstitutionCategory, 'PrivateCourse', 'unselected institution type stays unchanged');
      const nameOnlyCommit = await request(legacyInstitutionImportPath, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...nameOnlyImport,
          previewId: nameOnlyPreview.previewId, expectedWorkspaceName: nameOnlyPreview.workspaceName,
          expectedWorkspaceCategory: nameOnlyPreview.workspaceInstitutionCategory }) });
      assert.deepEqual(await nameOnlyCommit.json(), { previewId: nameOnlyPreview.previewId, name: 'Yeni kurum adı',
        institutionCategory: 'PrivateCourse', alreadyApplied: false });
      const addInstitutionMembers = queryCourseDb(`INSERT INTO "WorkspaceMemberships" ("WorkspaceId", "UserId", "Role", "IsActive") VALUES
        ('${institutionWorkspaceId}', '${institutionOwnerId}', 3, TRUE),
        ('${institutionWorkspaceId}', '${subject}', 2, TRUE),
        ('${institutionWorkspaceId}', '${otherTeacherId}', 2, TRUE)
        ON CONFLICT ("WorkspaceId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role", "IsActive" = TRUE;`);
      assert.equal(addInstitutionMembers.status, 0, addInstitutionMembers.stderr);
      const importedWorkspaceView = await request(`/api/course-management/v1/workspaces/${institutionWorkspaceId}`, institutionOwner);
      assert.deepEqual(await importedWorkspaceView.json(), { id: institutionWorkspaceId, name: 'Yeni kurum adı',
        type: 'Institution', institutionCategory: 'PrivateCourse', role: 'Owner', isReadOnly: false });
      const importedTeacherUserId = 'b96b47a8-2bb6-4ea1-86e6-c059d8f673ba';
      const importedTeacherInvitationId = 'ff38f2d2-9866-4815-8536-bdff134befce';
      const importedTeacherInvitePath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/invitations`;
      const importedTeacherInvite = await request(importedTeacherInvitePath, institutionOwner, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: importedTeacherInvitationId, targetUserId: importedTeacherUserId, role: 2 })
      });
      assert.equal(importedTeacherInvite.status, 201, await importedTeacherInvite.clone().text());
      assert.equal((await (await request(`/api/course-management/v1/workspaces/${institutionWorkspaceId}/members`, institutionOwner)).json())
        .some(member => member.userId === importedTeacherUserId), false,
        'a transferred source teacher is not active in the course workspace before accepting the invitation');
      assert.deepEqual(await (await request('/api/course-management/v1/participation/invitations',
        token('coaching', importedTeacherUserId, secret, 'Teacher'))).json().then(items => items.filter(item => item.id === importedTeacherInvitationId)
          .map(({ role, workspaceName }) => ({ role, workspaceName }))), [{ role: 'Teacher', workspaceName: 'Yeni kurum adı' }]);
      const importedTeacherAcceptance = await request(`/api/course-management/v1/participation/invitations/${importedTeacherInvitationId}/accept`,
        token('coaching', importedTeacherUserId, secret, 'Teacher'), {
          method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Aktarılan öğretmen' })
        });
      assert.equal(importedTeacherAcceptance.status, 200, await importedTeacherAcceptance.clone().text());
      assert.deepEqual(await importedTeacherAcceptance.json(), { workspaceId: institutionWorkspaceId, role: 'Teacher' });
      const institutionCourseId = '7b6b8591-a7a6-4f0f-aa74-128442d0c0c8';
      const institutionStudentId = '46e4157c-b8b0-4ba8-95a9-7cdb9c95777c';
      const institutionTeacherGroupId = '6c2070a4-cbcc-4a63-86bc-8e821582dc85';
      const ownerCreate = (path, body) => request(path, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      const institutionCoursePath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/courses`;
      const institutionStudentPath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/students`;
      const institutionGroupsPath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/groups`;
      assert.equal((await ownerCreate(institutionCoursePath, { id: institutionCourseId, name: 'Kurum matematik', subject: 'Matematik' })).status, 201);
      assert.equal((await ownerCreate(institutionStudentPath, { id: institutionStudentId, displayName: 'Kurum öğrencisi', email: null, phoneNumber: null })).status, 201);
      assert.equal((await ownerCreate(institutionGroupsPath, { id: institutionTeacherGroupId, courseId: institutionCourseId,
        teacherUserId: subject, name: 'Öğretmen grubu' })).status, 201);
      const institutionTeacherGroupStudentsPath = `${institutionGroupsPath}/${institutionTeacherGroupId}/students`;
      assert.equal((await request(`${institutionTeacherGroupStudentsPath}/${institutionStudentId}`, teacherToken, { method: 'POST' })).status, 201);
      const teacherStudentRoster = await request(institutionStudentPath, teacherToken);
      assert.equal(teacherStudentRoster.status, 200);
      assert.deepEqual((await teacherStudentRoster.json()).map(student => student.id), [institutionStudentId],
        'teacher student lists contain only active students in their assigned groups');
      const institutionGuardianPath = `${institutionStudentPath}/${institutionStudentId}/guardians`;
      const institutionGuardianId = 'b43822a2-8475-4bbd-9dc3-d85c9afe129c';
      assert.equal((await ownerCreate(institutionGuardianPath, { guardianId: institutionGuardianId, displayName: 'Kurum velisi',
        email: null, phoneNumber: '+90 555 000 00 00', relationship: 1 })).status, 201);
      const otherGroupStudentId = 'ad386b95-44a0-487a-a1e3-44767398feb2';
      const otherTeacherGroupId = '47d5c12c-8d9a-4799-b948-b8a46827fc02';
      assert.equal((await ownerCreate(institutionStudentPath, { id: otherGroupStudentId, displayName: 'Başka grubun öğrencisi', email: null, phoneNumber: null })).status, 201);
      assert.equal((await ownerCreate(institutionGroupsPath, { id: otherTeacherGroupId, courseId: institutionCourseId,
        teacherUserId: otherTeacherId, name: 'Diğer öğretmenin grubu' })).status, 201);
      assert.equal((await request(`${institutionGroupsPath}/${otherTeacherGroupId}/students/${otherGroupStudentId}`, institutionOwner, { method: 'POST' })).status, 201);
      const otherGroupGuardianId = '4caa593d-8783-4f6e-811a-b3f489e1a603';
      assert.equal((await ownerCreate(`${institutionStudentPath}/${otherGroupStudentId}/guardians`, { guardianId: otherGroupGuardianId,
        displayName: 'Başka grubun velisi', email: null, phoneNumber: null, relationship: 1 })).status, 201);

      assert.equal((await request(institutionStudentPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: 'bba349c2-148f-4c84-aa5f-f57db388f910', displayName: 'Öğretmen kaydı', email: null, phoneNumber: null }) })).status, 404,
        'institution teachers cannot create workspace student records');
      const institutionImportPath = `/api/course-management/v1/workspaces/${institutionWorkspaceId}/imports/students`;
      const institutionCsv = 'displayName,email,phoneNumber\nAktarım öğrencisi,aktarim@example.test,';
      const teacherImportPreview = await request(`${institutionImportPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: institutionCsv }) });
      assert.equal(teacherImportPreview.status, 404, 'institution teachers cannot preview workspace-wide CSV imports');
      const institutionImportPreview = await request(`${institutionImportPath}/preview`, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: institutionCsv }) });
      assert.equal(institutionImportPreview.status, 200, await institutionImportPreview.clone().text());
      const institutionImportPreviewBody = await institutionImportPreview.json();
      assert.equal((await request(institutionImportPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ csv: institutionCsv, previewId: institutionImportPreviewBody.previewId }) })).status, 404,
        'institution teachers cannot commit workspace-wide CSV imports');
      const ownerImport = await request(institutionImportPath, institutionOwner, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: institutionCsv, previewId: institutionImportPreviewBody.previewId }) });
      assert.equal(ownerImport.status, 200, 'workspace owners can commit a previewed CSV import');
      assert.equal((await request(`${institutionStudentPath}/${institutionStudentId}`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Öğretmen değiştirdi', email: null, phoneNumber: null, isActive: true }) })).status, 404,
        'institution teachers cannot change student profile records');
      assert.equal((await request(`${institutionStudentPath}/${institutionStudentId}`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Kurum öğrencisi', email: null, phoneNumber: null, isActive: false }) })).status, 404,
        'institution teachers cannot archive students');
      const teacherGuardianLinks = await request(`${institutionStudentPath}/${institutionStudentId}/guardians`, teacherToken);
      assert.equal(teacherGuardianLinks.status, 200, 'teachers can read guardian details linked to students in their own groups');
      assert.equal((await teacherGuardianLinks.json()).length, 1);
      const teacherGuardians = await request(`/api/course-management/v1/workspaces/${institutionWorkspaceId}/guardians`, teacherToken);
      assert.equal(teacherGuardians.status, 200, 'teachers can read their own groups’ linked guardian contacts');
      assert.deepEqual((await teacherGuardians.json()).map(guardian => guardian.id), [institutionGuardianId],
        'teacher guardian listings exclude contacts linked only to another teacher’s group');
      assert.equal((await request(institutionGuardianPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ guardianId: '70c55021-9548-4e96-9236-7f5309a8e269', displayName: 'Öğretmen velisi', email: null, phoneNumber: null, relationship: 1 }) })).status, 404,
        'only owners can create guardian records or student links');
      assert.equal((await request(`${institutionGuardianPath}/${institutionGuardianId}`, teacherToken, { method: 'DELETE' })).status, 404,
        'only owners can remove guardian links');
      assert.equal((await request(`/api/course-management/v1/workspaces/${institutionWorkspaceId}/guardians/${institutionGuardianId}`, teacherToken, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Öğretmen değiştirdi', email: null, phoneNumber: null }) })).status, 404,
        'only owners can edit guardian contact records');
      const anotherTeacherGroupId = '4f5a413a-6d91-40cf-83fb-a5d62eab4d7e';
      assert.equal((await request(institutionGroupsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: anotherTeacherGroupId, courseId: institutionCourseId, teacherUserId: otherTeacherId, name: 'Başka öğretmen grubu' }) })).status, 404,
        'teachers cannot assign groups to another teacher');
      const ownTeacherGroupId = '4f5a413a-6d91-40cf-83fb-a5d62eab4d7f';
      assert.equal((await request(institutionGroupsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: ownTeacherGroupId, courseId: institutionCourseId, teacherUserId: subject, name: 'Yeni kendi grubu' }) })).status, 201,
        'teachers can create groups assigned to themselves');
      const ownerAssignedGroupId = '4f5a413a-6d91-40cf-83fb-a5d62eab4d80';
      assert.equal((await ownerCreate(institutionGroupsPath, { id: ownerAssignedGroupId, courseId: institutionCourseId,
        teacherUserId: otherTeacherId, name: 'Owner ataması' })).status, 201,
        'owners can assign groups to another active teacher');
      assert.equal((await request(`/api/course-management/v1/workspaces/${institutionWorkspaceId}/guardians/${institutionGuardianId}`, institutionOwner, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Kurum velisi güncel', email: null, phoneNumber: '+90 555 000 00 01' }) })).status, 200,
        'workspace owners can edit guardian contact records');
      assert.equal((await request(`${institutionGuardianPath}/${institutionGuardianId}`, institutionOwner, { method: 'DELETE' })).status, 204,
        'workspace owners can remove guardian links');
      assert.equal((await request(`${institutionStudentPath}/${institutionStudentId}`, institutionOwner, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Kurum öğrencisi', email: null, phoneNumber: null, isActive: false }) })).status, 200,
        'workspace owners can archive student records');

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
      const subscriptionPayload = { planName: '  Öğretmen Plus  ', activeStudentLimit: 40,
        startsAt: '2026-10-01T00:00:00Z', endsAt: '2027-10-01T00:00:00Z', accessStatus: 1 };
      const setSubscription = payload => request(adminWorkspaceSubscriptionPath(workspaceId), token('coaching'), {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
      });
      const invalidSubscriptionAssignment = await setSubscription({ ...subscriptionPayload, activeStudentLimit: 0 });
      assert.equal(invalidSubscriptionAssignment.status, 400, 'invalid subscription limits are rejected');
      const unknownSubscriptionAssignment = await request(adminWorkspaceSubscriptionPath('00000000-0000-0000-0000-000000000001'), token('coaching'), {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(subscriptionPayload)
      });
      assert.equal(unknownSubscriptionAssignment.status, 404, 'unknown workspace subscriptions cannot be assigned');
      const assignedSubscription = await setSubscription(subscriptionPayload);
      assert.equal(assignedSubscription.status, 200, await assignedSubscription.clone().text());
      assert.deepEqual(await assignedSubscription.json(), {
        workspaceId, planName: 'Öğretmen Plus', activeStudentLimit: 40,
        startsAt: '2026-10-01T00:00:00+00:00', endsAt: '2027-10-01T00:00:00+00:00', accessStatus: 'Active'
      });
      const subscriptionPage = await request(`${adminSubscriptionsPath}?skip=0&take=10`, token('coaching'));
      assert.equal(subscriptionPage.status, 200, await subscriptionPage.clone().text());
      const subscriptionPageBody = await subscriptionPage.json();
      assert.ok(subscriptionPageBody.items.some(item => item.workspaceId === workspaceId
        && item.workspaceName === 'Özel ders alanım' && item.workspaceType === 'IndependentTeacher'
        && item.activeStudentCount === 0 && item.subscription?.planName === 'Öğretmen Plus'));
      assert.equal(Object.hasOwn(subscriptionPageBody.items[0], 'ownerUserId'), false,
        'subscription administration does not expose owner identity');
      assert.equal(Object.hasOwn(subscriptionPageBody.items[0], 'students'), false,
        'subscription administration does not expose student records');
      const ownSubscriptions = await request(mySubscriptionsPath, teacherToken);
      assert.equal(ownSubscriptions.status, 200, await ownSubscriptions.clone().text());
      const ownSubscriptionRows = await ownSubscriptions.json();
      assert.deepEqual(ownSubscriptionRows.find(item => item.workspaceId === workspaceId), {
        workspaceId, workspaceName: 'Özel ders alanım', workspaceType: 'IndependentTeacher', activeStudentCount: 0,
        subscription: { planName: 'Öğretmen Plus', activeStudentLimit: 40,
          startsAt: '2026-10-01T00:00:00+00:00', endsAt: '2027-10-01T00:00:00+00:00', accessStatus: 'Active' }
      });
      const unrelatedOwnerSubscriptions = await (await request(mySubscriptionsPath, courseStudent)).json();
      assert.ok(unrelatedOwnerSubscriptions.every(item => item.workspaceId !== workspaceId),
        'a different owner cannot see another workspace subscription');
      const updatedSubscription = await setSubscription({ ...subscriptionPayload, planName: 'Öğretmen Pro', accessStatus: 2 });
      assert.equal(updatedSubscription.status, 200);
      assert.equal((await updatedSubscription.json()).accessStatus, 'Suspended');
      const subscriptionAudit = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, teacherToken)).json();
      assert.deepEqual(subscriptionAudit.filter(entry => entry.action.startsWith('Subscription.')).map(entry => entry.action),
        ['Subscription.Updated', 'Subscription.Assigned']);
      assert.deepEqual(Object.keys(subscriptionAudit.find(entry => entry.action === 'Subscription.Assigned')).sort(),
        ['action', 'occurredAt'], 'subscription audit exposes only action and time to the workspace owner');
      assert.equal((await setSubscription(subscriptionPayload)).status, 200, 'SystemAdmin can reactivate the workspace');
      const createdWorkspaceAudit = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, teacherToken)).json();
      assert.equal(createdWorkspaceAudit.filter(entry => entry.action === 'Workspace.Created').length, 1);
      assert.deepEqual(Object.keys(createdWorkspaceAudit[0]).sort(), ['action', 'occurredAt'],
        'workspace audit response exposes only the action and timestamp');
      const subscriptionAuditPage = await request(`${adminSubscriptionAuditPath}?skip=0&take=2`, token('coaching'));
      assert.equal(subscriptionAuditPage.status, 200, await subscriptionAuditPage.clone().text());
      const firstAuditPage = await subscriptionAuditPage.json();
      assert.equal(firstAuditPage.totalCount, 3, 'only the three subscription changes are counted');
      assert.equal(firstAuditPage.items.length, 2, 'admin subscription audit uses bounded pages');
      assert.deepEqual(Object.keys(firstAuditPage.items[0]).sort(),
        ['action', 'occurredAt', 'workspaceName', 'workspaceType'],
        'admin audit does not expose workspace, actor, target or person identifiers');
      assert.ok(firstAuditPage.items.every(item => item.workspaceName === 'Özel ders alanım'
        && item.workspaceType === 'IndependentTeacher'
        && ['Subscription.Assigned', 'Subscription.Updated'].includes(item.action)));
      assert.ok(Date.parse(firstAuditPage.items[0].occurredAt) >= Date.parse(firstAuditPage.items[1].occurredAt),
        'admin subscription audit is ordered newest first');
      const secondAuditPage = await request(`${adminSubscriptionAuditPath}?skip=2&take=2`, token('coaching'));
      assert.equal(secondAuditPage.status, 200, await secondAuditPage.clone().text());
      const secondAuditPageBody = await secondAuditPage.json();
      assert.equal(secondAuditPageBody.totalCount, firstAuditPage.totalCount);
      assert.equal(secondAuditPageBody.items.length, 1);
      assert.notEqual(secondAuditPageBody.items[0].occurredAt, firstAuditPage.items[0].occurredAt,
        'audit pagination does not repeat the first page record');
      const csv = 'email,displayName,phoneNumber\nada@example.test,"Ada, Öğrenci",+90 555 000 00 00\nmehmet@example.test,Mehmet Öğrenci,';
      const importPath = `/api/course-management/v1/workspaces/${workspaceId}/imports/students`;
      const preview = await request(`${importPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv }) });
      assert.equal(preview.status, 200, await preview.clone().text());
      const previewBody = await preview.json();
      assert.equal(previewBody.readyCount, 2);
      assert.equal(previewBody.rows[0].displayName, 'Ada, Öğrenci');
      assert.equal(previewBody.rows[0].status, 'Ready');
      const commitImport = () => request(importPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv, previewId: previewBody.previewId }) });
      const committedImport = await commitImport();
      assert.equal(committedImport.status, 200);
      assert.deepEqual(await committedImport.json(), { previewId: previewBody.previewId, createdCount: 2, alreadyImportedCount: 0 });
      const overLimitUpdate = await setSubscription({ ...subscriptionPayload, activeStudentLimit: 1 });
      assert.equal(overLimitUpdate.status, 409, 'an existing active student count cannot exceed a newly assigned plan limit');
      assert.equal((await (await request(mySubscriptionsPath, teacherToken)).json())
        .find(item => item.workspaceId === workspaceId).subscription.activeStudentLimit, 40,
        'a rejected limit update leaves the prior plan unchanged');
      const repeatedImport = await commitImport();
      assert.equal(repeatedImport.status, 200, 'repeating the same preview commit is safe');
      assert.deepEqual(await repeatedImport.json(), { previewId: previewBody.previewId, createdCount: 0, alreadyImportedCount: 2 });
      const importedStudents = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json();
      assert.ok(importedStudents.every(student => student.userId === null), 'CSV import creates local records without linking Identity accounts by email or name');
      const adaStudent = importedStudents.find(student => student.email === 'ada@example.test');
      assert.ok(adaStudent);
      const legacyWorkspaceId = '69f2fd68-3816-4914-b75c-5adf8a182e27';
      const createLegacyWorkspace = await request('/api/course-management/v1/workspaces', teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: legacyWorkspaceId, name: 'Aktarım kabul alanı', type: 2 })
      });
      assert.equal(createLegacyWorkspace.status, 201);
      const legacyStudentsPath = `/api/course-management/v1/workspaces/${legacyWorkspaceId}/students`;
      const existingLegacyStudentId = '98f2fd68-3816-4914-b75c-5adf8a182e27';
      assert.equal((await request(legacyStudentsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: existingLegacyStudentId, displayName: 'Önceden yerel öğrenci', email: 'legacy-same@example.test', phoneNumber: null }) })).status, 201);
      const legacyImportPath = `/api/course-management/v1/workspaces/${legacyWorkspaceId}/imports/legacy-students`;
      const legacyRows = [
        { sourceRecordId: '2395e226-2a8d-4e7d-bcf3-8c21f4e88f1a', displayName: 'Aynı e-postalı öğrenci', email: 'legacy-same@example.test', isActive: true },
        { sourceRecordId: 'f03a0a7b-4e7a-4c2c-91f3-679da14a9a33', displayName: 'Aktarılacak öğrenci', email: 'legacy-new@example.test', isActive: false }
      ];
      const previewLegacy = (sourceSystem, selectedFields) => request(`${legacyImportPath}/preview`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourceSystem, selectedFields, rows: legacyRows })
      });
      assert.equal((await previewLegacy('speed-reading', ['displayName', 'email'])).status, 200,
        'workspace owner can preview selected fields from a registered source');
      const conflictingLegacyPreview = await (await previewLegacy('speed-reading', ['displayName', 'email'])).json();
      assert.equal(conflictingLegacyPreview.rows[0].status, 'Conflict', 'email collision is surfaced and never linked automatically');
      const safeLegacyPreviewResponse = await previewLegacy('speed-reading', ['displayName']);
      const safeLegacyPreview = await safeLegacyPreviewResponse.json();
      assert.equal(safeLegacyPreview.readyCount, 2, 'owner can omit the conflicting email field and retain only selected fields');
      const commitLegacy = () => request(legacyImportPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourceSystem: 'speed-reading', selectedFields: ['displayName'], rows: legacyRows, previewId: safeLegacyPreview.previewId }) });
      const committedLegacy = await commitLegacy();
      assert.equal(committedLegacy.status, 200, await committedLegacy.clone().text());
      assert.deepEqual(await committedLegacy.json(), { previewId: safeLegacyPreview.previewId, createdCount: 2, alreadyImportedCount: 0 });
      const importedLegacyStudents = await (await request(legacyStudentsPath, teacherToken)).json();
      const importedLegacyStudent = importedLegacyStudents.find(student => student.displayName === 'Aktarılacak öğrenci');
      assert.ok(importedLegacyStudent);
      assert.equal(importedLegacyStudent.email, null, 'unselected source email is never copied');
      assert.equal(importedLegacyStudent.userId, null, 'source account identity is never linked automatically');
      assert.deepEqual(await (await commitLegacy()).json(), {
        previewId: safeLegacyPreview.previewId, createdCount: 0, alreadyImportedCount: 2
      }, 'repeating a legacy import is idempotent by source record identity');
      assert.equal((await previewLegacy('unknown', ['displayName'])).status, 400, 'only supported source products may be imported');
      assert.equal((await request(`${legacyImportPath}/preview`, courseStudent, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourceSystem: 'speed-reading', selectedFields: ['displayName'], rows: legacyRows }) })).status, 404,
        'non-owners cannot preview legacy imports');
      const createQuotaStudent = (id, displayName, email) => request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, displayName, email, phoneNumber: null })
      });
      assert.equal((await setSubscription({ ...subscriptionPayload, activeStudentLimit: 3 })).status, 200);
      const concurrentStudentIds = ['1b2096e7-42dd-4ff2-92a4-f932711bd006', '5f51fb0c-51f4-4bba-8398-788e3d509a75'];
      const concurrentStudentCreates = await Promise.all(concurrentStudentIds.map((id, index) =>
        createQuotaStudent(id, `Eşzamanlı kapasite ${index + 1}`, `concurrent-${index + 1}@example.test`)));
      assert.deepEqual(concurrentStudentCreates.map(result => result.status).sort(), [201, 409],
        'workspace row locking prevents concurrent requests from exceeding the active student limit');
      const concurrentQuotaCleanup = queryCourseDb(`DELETE FROM "WorkspaceAuditEntries" WHERE "TargetId" IN ('${concurrentStudentIds.join("','")}'); DELETE FROM "Students" WHERE "Id" IN ('${concurrentStudentIds.join("','")}');`);
      assert.equal(concurrentQuotaCleanup.status, 0, concurrentQuotaCleanup.stderr);
      assert.equal((await setSubscription({ ...subscriptionPayload, activeStudentLimit: 2 })).status, 200);
      const blockedStudent = await createQuotaStudent('0f6c5d8d-6a71-46ab-9daa-80463795da21', 'Sınır üstü öğrenci', 'over-limit@example.test');
      assert.equal(blockedStudent.status, 409, 'manual student creation respects the active student limit');
      const quotaCsv = 'displayName,email,phoneNumber\nKapasite üç,capacity-3@example.test,\nKapasite dört,capacity-4@example.test,';
      const quotaPreview = await request(`${importPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: quotaCsv }) });
      const quotaPreviewBody = await quotaPreview.json();
      const blockedImport = await request(importPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: quotaCsv, previewId: quotaPreviewBody.previewId }) });
      assert.equal(blockedImport.status, 409, 'CSV import rejects an over-limit batch atomically');
      assert.equal((await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json()).length, 2,
        'rejected quota operations do not create partial student records');
      const updateStudent = (student, isActive) => request(`/api/course-management/v1/workspaces/${workspaceId}/students/${student.id}`, teacherToken, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: student.displayName, email: student.email, phoneNumber: student.phoneNumber, isActive })
      });
      assert.equal((await updateStudent(adaStudent, false)).status, 200);
      const quotaFillId = 'e7db9094-ff22-4418-9b23-df3188d9b911';
      assert.equal((await createQuotaStudent(quotaFillId, 'Geçici kapasite öğrencisi', 'quota-fill@example.test')).status, 201);
      assert.equal((await updateStudent(adaStudent, true)).status, 409, 'reactivating a student respects the active student limit');
      const quotaCleanup = queryCourseDb(`DELETE FROM "WorkspaceAuditEntries" WHERE "TargetId" = '${quotaFillId}'; DELETE FROM "Students" WHERE "Id" = '${quotaFillId}';`);
      assert.equal(quotaCleanup.status, 0, quotaCleanup.stderr);
      assert.equal((await updateStudent(adaStudent, true)).status, 200, 'a student can be restored when capacity is available');
      assert.equal((await setSubscription(subscriptionPayload)).status, 200);
      const duplicateCsv = 'displayName,email,phoneNumber\nBaşka kayıt,ADA@EXAMPLE.TEST,';
      const duplicatePreview = await request(`${importPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: duplicateCsv }) });
      const duplicatePreviewBody = await duplicatePreview.json();
      assert.equal(duplicatePreviewBody.duplicateCount, 1);
      const duplicateCommit = await request(importPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: duplicateCsv, previewId: duplicatePreviewBody.previewId }) });
      assert.equal(duplicateCommit.status, 400, 'rows with duplicates cannot be committed');
      const repeatedRowsCsv = 'displayName,email,phoneNumber\nİlk öğrenci,repeated@example.test,\nİkinci öğrenci,REPEATED@example.test,';
      const repeatedRowsPreview = await request(`${importPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: repeatedRowsCsv }) });
      assert.equal((await repeatedRowsPreview.json()).duplicateCount, 1, 'duplicate rows within the same file are reported');
      const invalidCsv = 'displayName,email,phoneNumber\n,invalid@example.test,\nGeçerli öğrenci,valid@example.test,';
      const invalidPreview = await request(`${importPath}/preview`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: invalidCsv }) });
      const invalidPreviewBody = await invalidPreview.json();
      assert.equal(invalidPreviewBody.invalidCount, 1);
      assert.equal(invalidPreviewBody.readyCount, 1);
      assert.equal((await request(importPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: invalidCsv, previewId: invalidPreviewBody.previewId }) })).status, 400,
        'invalid rows block the whole import instead of partially importing valid rows');
      assert.equal((await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json()).length, 2,
        'rejected imports do not partially add students');
      assert.equal((await request(`${importPath}/preview`, courseStudent, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv }) })).status, 404,
        'another workspace member cannot import through an unrelated workspace');
      const invitationId = 'ba5a0d8d-15d3-4212-ad98-1b10d32b110f';
      const invitePath = `/api/course-management/v1/workspaces/${workspaceId}/invitations`;
      const selectedStudentRecordId = '83a32dc1-199d-499f-ad85-84ea567d5b93';
      const createSelectedStudentRecord = () => request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: selectedStudentRecordId, displayName: 'Eşleşecek öğrenci', email: 'local@example.test', phoneNumber: null })
      });
      assert.equal((await createSelectedStudentRecord()).status, 201);
      const createInvite = (bearer, id = invitationId, role = 1, studentRecordId = null) => request(invitePath, bearer, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id, targetUserId: otherUser, role, studentRecordId })
      });
      assert.equal((await createInvite(courseStudent)).status, 404);
      assert.equal((await createInvite(teacherToken, invitationId, 3)).status, 400, 'invitations cannot grant owner');
      assert.equal((await createInvite(teacherToken, '810d378d-78d4-47a0-a788-f81e85d281cf', 2, selectedStudentRecordId)).status, 400,
        'student records cannot be attached to teacher invitations');
      assert.equal((await createInvite(teacherToken, 'b79336e2-6d63-49bf-9aa6-8f5a29a9ec8b', 1, institutionStudentId)).status, 409,
        'an owner cannot attach another workspace’s student record');
      assert.equal((await createInvite(teacherToken)).status, 201);
      assert.equal((await createInvite(teacherToken)).status, 200);
      const oldStudent = token('speed-reading', otherUser, secret, 'Student', 'local@example.test');
      const pending = await (await request('/api/course-management/v1/participation/invitations', oldStudent)).json();
      assert.equal(pending.length, 1);
      assert.equal(pending[0].id, invitationId);
      const acceptPath = `/api/course-management/v1/participation/invitations/${invitationId}/accept`;
      const accept = bearer => request(acceptPath, bearer, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Davetli öğrenci' }) });
      assert.equal((await accept(teacherToken)).status, 404, 'only the named recipient can accept');
      const missingOwnerSelection = await accept(oldStudent);
      assert.equal(missingOwnerSelection.status, 409, 'matching an existing local student requires the owner to select that record');
      assert.deepEqual(await missingOwnerSelection.json(), { code: 'Student.RecordSelectionRequired' });
      const unlinkedStudents = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json();
      assert.equal(unlinkedStudents.find(student => student.id === selectedStudentRecordId).userId, null,
        'failed acceptance never links a student by email');
      assert.equal((await request(`${invitePath}/${invitationId}/cancel`, teacherToken, { method: 'POST' })).status, 204);
      const selectedInvitationId = '7d68c3f9-3f5c-4705-9d78-73f3ba4b915f';
      const selectedInvitation = await createInvite(teacherToken, selectedInvitationId, 1, selectedStudentRecordId);
      assert.equal(selectedInvitation.status, 201, await selectedInvitation.clone().text());
      const selectedAcceptPath = `/api/course-management/v1/participation/invitations/${selectedInvitationId}/accept`;
      const acceptSelected = bearer => request(selectedAcceptPath, bearer, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Davetli öğrenci' }) });
      const accepts = await Promise.all([acceptSelected(oldStudent), acceptSelected(oldStudent)]);
      for (const accepted of accepts) assert.ok([200, 409].includes(accepted.status),
        `unexpected concurrent accept status ${accepted.status}: ${await accepted.clone().text()}`);
      assert.equal((await acceptSelected(oldStudent)).status, 200);
      assert.equal((await createInvite(teacherToken, selectedInvitationId, 1, selectedStudentRecordId)).status, 200,
        'repeating an accepted explicit-link invite remains idempotent');
      const studentSubscriptions = await (await request(mySubscriptionsPath, courseStudent)).json();
      assert.ok(studentSubscriptions.every(item => item.workspaceId !== workspaceId),
        'a student sees no subscription for a workspace they do not own');
      const acceptedInvitationAudit = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, teacherToken)).json();
      assert.equal(acceptedInvitationAudit.filter(entry => entry.action === 'Invitation.Accepted').length, 1,
        'idempotent invitation retries do not create additional audit entries');
      assert.equal((await setSubscription({ ...subscriptionPayload, activeStudentLimit: 3 })).status, 200);
      const secondStudentId = '2a7fef51-fb37-44d0-8ed4-5d85eab62ce3';
      const secondInvitationId = 'be5cc592-1303-4318-a2d6-0e6171dd3711';
      assert.equal((await request(invitePath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: secondInvitationId, targetUserId: secondStudentId, role: 1 }) })).status, 201);
      const secondStudentAcceptPath = `/api/course-management/v1/participation/invitations/${secondInvitationId}/accept`;
      const secondStudentAccept = () => request(secondStudentAcceptPath,
        token('coaching', secondStudentId, secret, 'Student'), { method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ displayName: 'Başka öğrenci' }) });
      const overLimitAcceptance = await secondStudentAccept();
      assert.equal(overLimitAcceptance.status, 409, 'Identity invitation acceptance enforces the active student limit');
      assert.deepEqual(await overLimitAcceptance.json(), { code: 'Student.ActiveLimitReached' });
      assert.equal((await setSubscription({ ...subscriptionPayload, activeStudentLimit: 4 })).status, 200);
      const secondStudentAccepted = await secondStudentAccept();
      assert.equal(secondStudentAccepted.status, 200, await secondStudentAccepted.clone().text());
      const studentRosterAfterSecondInvite = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json();
      assert.ok(studentRosterAfterSecondInvite.some(student => student.userId === secondStudentId && student.displayName === 'Başka öğrenci'),
        'an invitation with no matching local record creates a new student record');
      const courseId = 'a6e6651d-3d99-48a1-af8a-b774f4bf3225';
      const groupId = '25e175a6-79ef-41da-b575-39ff7cc649cb';
      const coursePath = `/api/course-management/v1/workspaces/${workspaceId}/courses`;
      assert.equal((await request(coursePath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: courseId, name: 'Matematik', subject: 'Matematik' }) })).status, 201);
      const manualStudentId = 'a316efca-1349-4b04-8489-41a251938827';
      const createManualStudent = () => request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: manualStudentId, displayName: 'Hesapsız öğrenci', email: 'unlinked@example.test', phoneNumber: null })
      });
      assert.equal((await setSubscription({ ...subscriptionPayload, activeStudentLimit: 5 })).status, 200);
      assert.equal((await createManualStudent()).status, 201);
      assert.equal((await setSubscription(subscriptionPayload)).status, 200);
      const createdGroup = await request(`/api/course-management/v1/workspaces/${workspaceId}/groups`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: groupId, courseId, teacherUserId: subject, name: '8. sınıf' })
      });
      assert.equal(createdGroup.status, 201);
      const groupPath = `/api/course-management/v1/workspaces/${workspaceId}/groups/${groupId}/students`;
      assert.equal((await request(`${groupPath}/${manualStudentId}`, teacherToken, { method: 'POST' })).status, 201);
      const roster = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json();
      const firstStudentPage = await request(`/api/course-management/v1/workspaces/${workspaceId}/students?skip=0&take=1`, teacherToken);
      assert.equal(firstStudentPage.status, 200);
      assert.equal((await firstStudentPage.json()).length, 1, 'student lists return a requested page');
      const nextStudentPage = await request(`/api/course-management/v1/workspaces/${workspaceId}/students?skip=1&take=1`, teacherToken);
      assert.equal(nextStudentPage.status, 200);
      assert.notEqual((await nextStudentPage.json())[0].id, (await request(`/api/course-management/v1/workspaces/${workspaceId}/students?skip=0&take=1`, teacherToken).then(response => response.json()))[0].id,
        'student pages do not repeat the first page');
      assert.equal((await request(`/api/course-management/v1/workspaces/${workspaceId}/students?take=201`, teacherToken)).status, 400,
        'student page size cannot exceed the endpoint limit');
      const linkedStudentId = roster.find(student => student.userId === otherUser)?.id;
      assert.ok(linkedStudentId, 'accepting a student invite links a local student record to the account');
      assert.equal(linkedStudentId, selectedStudentRecordId, 'the owner-selected record is linked rather than a new email-matched record');
      assert.equal((await request(`${groupPath}/${linkedStudentId}`, teacherToken, { method: 'POST' })).status, 201);
      const studentCourses = await (await request('/api/course-management/v1/me/courses', courseStudent)).json();
      assert.equal(studentCourses.length, 1);
      assert.equal(studentCourses[0].name, 'Matematik');
      const assignmentId = 'a88e5d9a-14ab-4ee5-94a9-9dd7698ad309';
      const assignmentsPath = `/api/course-management/v1/workspaces/${workspaceId}/assignments`;
      const assignmentDueAt = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString();
      const createAssignment = (body, bearer = teacherToken) => request(assignmentsPath, bearer, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      const assignment = await createAssignment({ id: assignmentId, courseId, groupId, studentId: null,
        title: 'Kesirler çalışma kâğıdı', instructions: 'İşlemleri açıklayarak çözün.', dueAt: assignmentDueAt });
      assert.equal(assignment.status, 201, await assignment.clone().text());
      assert.equal((await request(`${assignmentsPath}?skip=0&take=1`, teacherToken)).status, 200);
      assert.equal((await request(`${assignmentsPath}?skip=1&take=1`, teacherToken).then(response => response.json())).length, 0,
        'assignment report paging does not repeat its first page');
      assert.equal((await request(`${assignmentsPath}?take=201`, teacherToken)).status, 400,
        'assignment page size cannot exceed the endpoint limit');
      const assignmentReminders = readReminders(assignmentId);
      assert.equal(assignmentReminders.length, 1, 'linked assignment recipients receive one scheduled reminder');
      assert.equal(assignmentReminders[0].type, 2);
      assert.equal(assignmentReminders[0].status, 1);
      assert.equal(Date.parse(assignmentReminders[0].targetAt), Date.parse(assignmentDueAt));
      assert.equal(Date.parse(assignmentReminders[0].notifyAt), Date.parse(assignmentDueAt) - 24 * 60 * 60 * 1000);
      const publishedOutboxCount = spawnSync('docker', ['exec', container, 'psql', '-U', 'course_test', '-d', 'course_test', '-tAc',
        `SELECT COUNT(*) FROM "CourseOutboxTestAudit" WHERE "MessageType" LIKE '%CourseManagementAssignmentPublishedV1%';`], { encoding: 'utf8' });
      assert.equal(publishedOutboxCount.status, 0, publishedOutboxCount.stderr);
      assert.equal(publishedOutboxCount.stdout.trim(), '1', 'assignment and publication event commit together to the service outbox');
      assert.equal((await createAssignment({ id: assignmentId, courseId, groupId, studentId: null,
        title: 'Kesirler çalışma kâğıdı', instructions: 'İşlemleri açıklayarak çözün.', dueAt: assignmentDueAt })).status, 200);
      const idempotentPublishedOutboxCount = spawnSync('docker', ['exec', container, 'psql', '-U', 'course_test', '-d', 'course_test', '-tAc',
        `SELECT COUNT(*) FROM "CourseOutboxTestAudit" WHERE "MessageType" LIKE '%CourseManagementAssignmentPublishedV1%';`], { encoding: 'utf8' });
      assert.equal(idempotentPublishedOutboxCount.status, 0, idempotentPublishedOutboxCount.stderr);
      assert.equal(idempotentPublishedOutboxCount.stdout.trim(), '1', 'replaying the assignment request does not enqueue a duplicate event');
      const makeAssignmentReminderDue = queryCourseDb(`UPDATE "CourseManagementReminders" SET "NotifyAt" = NOW() - INTERVAL '1 minute' WHERE "RelatedEntityId" = '${assignmentId}' AND "Status" = 1;`);
      assert.equal(makeAssignmentReminderDue.status, 0, makeAssignmentReminderDue.stderr);
      let reminderQueueState;
      for (let attempt = 0; attempt < 40; attempt++) {
        const state = queryCourseDb(`SELECT r."Status" || ':' || (SELECT COUNT(*) FROM "CourseOutboxTestAudit" a WHERE a."MessageType" LIKE '%CourseManagementReminderDueV1%') FROM "CourseManagementReminders" r WHERE r."RelatedEntityId" = '${assignmentId}' AND r."Type" = 2 ORDER BY r."CreatedAt" DESC LIMIT 1;`);
        assert.equal(state.status, 0, state.stderr);
        reminderQueueState = state.stdout.trim();
        if (reminderQueueState.startsWith('3:')) break;
        await new Promise(resolve => setTimeout(resolve, 250));
      }
      assert.equal(reminderQueueState, '3:1', 'worker atomically queues one reminder event through the service outbox');
      const assignmentAttachmentsPath = `${assignmentsPath}/${assignmentId}/attachments`;
      const infectedUpload = await request(`${assignmentAttachmentsPath}?fileName=test-virus.pdf`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' },
        body: Buffer.from(`%PDF-1.4\n1 0 obj\n<< /Length ${Buffer.byteLength(eicarSignature)} >>\nstream\n${eicarSignature}\nendstream\nendobj\n%%EOF`)
      });
      assert.equal(infectedUpload.status, 422, await infectedUpload.clone().text());
      assert.deepEqual(await infectedUpload.json(), { code: 'AssignmentAttachment.Rejected' });
      assert.equal(await countFiles(attachmentRoot), 0, 'rejected uploads are deleted before the response completes');
      if (!useExternalClam) {
        const failedScanUpload = await request(`${assignmentAttachmentsPath}?fileName=scan-error.pdf`, teacherToken, {
          method: 'POST', headers: { 'Content-Type': 'application/pdf' },
          body: Buffer.from('%PDF-1.4\nSIMULATED-SCAN-ERROR\n%%EOF')
        });
        assert.equal(failedScanUpload.status, 503, await failedScanUpload.clone().text());
        assert.equal(await countFiles(attachmentRoot), 0, 'scanner failures remain fail-closed and delete stored content');

        const abortController = new AbortController();
        const cancelledUpload = request(`${assignmentAttachmentsPath}?fileName=cancelled-scan.pdf`, teacherToken, {
          method: 'POST', headers: { 'Content-Type': 'application/pdf' },
          body: Buffer.from('%PDF-1.4\nSIMULATED-SCAN-CANCEL\n%%EOF'), signal: abortController.signal
        }).then(response => ({ response }), error => ({ error }));
        let cancellationTimer;
        try {
          await Promise.race([cancellationScanStarted, new Promise(resolve => {
            cancellationTimer = setTimeout(resolve, 5000);
          })]);
        } finally {
          clearTimeout(cancellationTimer);
        }
        assert.equal(cancellationScanObserved, true, 'upload reached the scanner before cancellation');
        abortController.abort();
        const cancellationResult = await cancelledUpload;
        assert.equal(cancellationResult.error?.name, 'AbortError', 'client cancellation closes the upload request');
        for (let attempt = 0; attempt < 40 && await countFiles(attachmentRoot) > 0; attempt++)
          await new Promise(resolve => setTimeout(resolve, 50));
        assert.equal(await countFiles(attachmentRoot), 0, 'cancelled scans delete stored content');
      }
      const teacherMaterialUpload = await request(`${assignmentAttachmentsPath}?fileName=calisma-kagidi.pdf`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' }, body: Buffer.from('%PDF-1.4\ncourse material\n')
      });
      assert.equal(teacherMaterialUpload.status, 201, await teacherMaterialUpload.clone().text());
      assert.equal(await countFiles(attachmentRoot), 1, 'clean scanned uploads are retained');
      const teacherMaterial = await teacherMaterialUpload.json();
      assert.equal(teacherMaterial.purpose, 'TeacherMaterial');
      assert.equal(Date.parse(teacherMaterial.expiresAt), new Date(assignmentDueAt).getTime() + 180 * 86400000);
      assert.equal(Object.hasOwn(teacherMaterial, 'storageKey'), false, 'private storage keys are never exposed');
      assert.equal((await request(`${assignmentAttachmentsPath}?fileName=calisma-kagidi.exe`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' }, body: Buffer.from('%PDF-1.4\ncontent')
      })).status, 400, 'unsupported extensions are rejected');
      assert.equal((await request(`${assignmentAttachmentsPath}?fileName=taklit.pdf`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' }, body: Buffer.from('not a PDF')
      })).status, 400, 'file signatures must match their declared type');
      const oversizedPdf = Buffer.alloc(10 * 1024 * 1024 + 1, 0x20);
      oversizedPdf.write('%PDF-1.4\n');
      assert.equal((await request(`${assignmentAttachmentsPath}?fileName=buyuk.pdf`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' }, body: oversizedPdf
      })).status, 413, 'attachment uploads are limited to 10 MB');
      const invalidAssignment = await createAssignment({ id: '1125dfd0-8aed-4ded-a76a-86a929bed69c', courseId,
        groupId, studentId: null, title: 'Tarihsiz ödev', instructions: '', dueAt: null });
      assert.equal(invalidAssignment.status, 400, 'assignment due date is required');
      assert.equal((await createAssignment({ id: 'c68df5d0-8977-4870-a8b9-146e2e93cc1e', courseId, groupId,
        studentId: null, title: 'Yetkisiz', instructions: '', dueAt: assignmentDueAt }, courseStudent)).status, 404,
        'students cannot create assignments');
      const studentAssignments = await request('/api/course-management/v1/me/assignments', courseStudent);
      assert.equal(studentAssignments.status, 200, await studentAssignments.clone().text());
      const visibleAssignment = (await studentAssignments.json()).find(item => item.id === assignmentId);
      assert.equal(visibleAssignment.title, 'Kesirler çalışma kâğıdı');
      assert.equal(visibleAssignment.status, 'Pending');
      assert.deepEqual(visibleAssignment.materials.map(item => item.id), [teacherMaterial.id]);
      const teacherMaterialDownloadPath = `/api/course-management/v1/me/assignments/${assignmentId}/attachments/${teacherMaterial.id}/content`;
      assert.equal((await request(teacherMaterialDownloadPath, courseStudent)).status, 200);
      assert.equal((await request(teacherMaterialDownloadPath, token('course-management', '71e6d307-23cc-4200-9e4b-16ada95eed59'))).status, 404,
        'students without an assignment recipient record cannot download its files');
      const submitAssignment = () => request(`/api/course-management/v1/me/assignments/${assignmentId}/submission`, courseStudent, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text: '1/2 + 1/4 = 3/4' }) });
      const submission = await submitAssignment();
      assert.equal(submission.status, 201, await submission.clone().text());
      const studentFileUpload = await request(`/api/course-management/v1/me/assignments/${assignmentId}/attachments?fileName=cozum.pdf`, courseStudent, {
        method: 'POST', headers: { 'Content-Type': 'application/pdf' }, body: Buffer.from('%PDF-1.4\nstudent answer\n')
      });
      assert.equal(studentFileUpload.status, 201, await studentFileUpload.clone().text());
      const studentFile = await studentFileUpload.json();
      assert.equal(studentFile.purpose, 'StudentSubmission');
      const ownAssignmentAfterUpload = (await (await request('/api/course-management/v1/me/assignments', courseStudent)).json())
        .find(item => item.id === assignmentId);
      assert.deepEqual(ownAssignmentAfterUpload.submissionAttachments.map(item => item.id), [studentFile.id]);
      const studentFileDownloadPath = `/api/course-management/v1/me/assignments/${assignmentId}/attachments/${studentFile.id}/content`;
      const studentFileDownload = await request(studentFileDownloadPath, courseStudent);
      assert.equal(studentFileDownload.status, 200);
      assert.match(await studentFileDownload.text(), /^%PDF-1\.4/);
      assert.equal((await request(studentFileDownloadPath, token('course-management', '71e6d307-23cc-4200-9e4b-16ada95eed59'))).status, 404);
      const teacherSubmissions = await request(`${assignmentsPath}/${assignmentId}/submissions`, teacherToken);
      assert.equal(teacherSubmissions.status, 200);
      assert.deepEqual((await teacherSubmissions.json()).find(item => item.studentId === linkedStudentId).attachments
        .map(item => item.id), [studentFile.id]);
      const expiredAttachment = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "CourseAssignmentAttachments" SET "ExpiresAt" = NOW() WHERE "Id" = '${teacherMaterial.id}';`, encoding: 'utf8'
      });
      assert.equal(expiredAttachment.status, 0, expiredAttachment.stderr);
      assert.equal((await request(teacherMaterialDownloadPath, courseStudent)).status, 404,
        'expired attachment content is unavailable even before the cleanup job runs');
      const gradePath = `${assignmentsPath}/${assignmentId}/submissions/${linkedStudentId}/grade`;
      const grade = await request(gradePath, teacherToken, { method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ score: 95, feedback: 'Çözümün doğru.' }) });
      assert.equal(grade.status, 200, await grade.clone().text());
      const reviewedAssignment = (await (await request('/api/course-management/v1/me/assignments', courseStudent)).json())
        .find(item => item.id === assignmentId);
      assert.equal(reviewedAssignment.status, 'Reviewed');
      assert.equal(reviewedAssignment.score, 95);
      assert.equal(reviewedAssignment.feedback, 'Çözümün doğru.');
      const guardianPath = `/api/course-management/v1/workspaces/${workspaceId}/students/${manualStudentId}/guardians`;
      const guardian = await request(guardianPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ guardianId: '9f9f37ee-190f-46b4-a48d-2f1a1e3e51aa', displayName: 'Öğrenci velisi',
          email: null, phoneNumber: '+90 555 000 00 00', relationship: 1 }) });
      assert.equal(guardian.status, 201);
      assert.equal((await request(`/api/course-management/v1/workspaces/0b4ed4ad-43d0-4711-9da9-ed26578607bf/students/${manualStudentId}/guardians`, teacherToken, {
        method: 'GET' })).status, 404, 'guardian contacts cannot be read across workspaces');
      const archived = await request(`/api/course-management/v1/workspaces/${workspaceId}/students/${manualStudentId}`, teacherToken, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Hesapsız öğrenci', email: 'unlinked@example.test', phoneNumber: null, isActive: false })
      });
      assert.equal(archived.status, 200, await archived.clone().text());
      assert.equal((await archived.json()).isActive, false);
      assert.equal((await (await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken)).json())
        .find(student => student.id === manualStudentId).isActive, false, 'archived records remain in roster history');
      assert.equal((await createManualStudent()).status, 409, 'student create retries do not duplicate records');
      const lessonPath = `/api/course-management/v1/workspaces/${workspaceId}/lessons`;
      const staffCreditPath = `/api/course-management/v1/workspaces/${workspaceId}/makeup-credits`;
      const studentCreditPath = '/api/course-management/v1/me/makeup-credits';
      const scheduledAt = days => {
        const value = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
        value.setUTCMinutes(0, 0, 0);
        return value;
      };
      const createLesson = async (id, startsAt) => request(lessonPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id, courseId, groupId, studentId: null,
          teacherUserId: subject, startsAt: startsAt.toISOString(), endsAt: new Date(startsAt.getTime() + 60 * 60 * 1000).toISOString() }) });
      const movableLessonId = 'ba8b1ce1-6db2-48cb-badd-a5d4cc1c7ec8';
      const blockingLessonId = 'eb4ef70e-292f-4954-a50c-84922490f9c0';
      const originalStartsAt = scheduledAt(2);
      const blockingStartsAt = scheduledAt(5);
      const movableLesson = await createLesson(movableLessonId, originalStartsAt);
      assert.equal(movableLesson.status, 201, await movableLesson.clone().text());
      const originalLessonReminders = readReminders(movableLessonId);
      assert.equal(originalLessonReminders.length, 1);
      assert.equal(originalLessonReminders[0].type, 1);
      assert.equal(Date.parse(originalLessonReminders[0].targetAt), originalStartsAt.getTime());
      assert.equal(Date.parse(originalLessonReminders[0].notifyAt), originalStartsAt.getTime() - 24 * 60 * 60 * 1000);
      const blockingLesson = await createLesson(blockingLessonId, blockingStartsAt);
      assert.equal(blockingLesson.status, 201, await blockingLesson.clone().text());
      const lessonSchedulePath = `${lessonPath}/${movableLessonId}/schedule`;
      const rescheduleLesson = startsAt => request(lessonSchedulePath, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ startsAt: startsAt.toISOString(),
          endsAt: new Date(startsAt.getTime() + 60 * 60 * 1000).toISOString(), roomId: null }) });
      const movedStartsAt = scheduledAt(3);
      assert.equal((await rescheduleLesson(movedStartsAt)).status, 200, 'an upcoming standalone lesson can be moved');
      const rescheduledLessonReminders = readReminders(movableLessonId);
      assert.equal(rescheduledLessonReminders.length, 2, 'rescheduling keeps an audit row and adds the new schedule');
      assert.equal(rescheduledLessonReminders.find(item => Date.parse(item.targetAt) === originalStartsAt.getTime()).status, 2);
      const activeRescheduledReminder = rescheduledLessonReminders.find(item => Date.parse(item.targetAt) === movedStartsAt.getTime());
      assert.equal(activeRescheduledReminder.status, 1);
      assert.equal(Date.parse(activeRescheduledReminder.notifyAt), movedStartsAt.getTime() - 24 * 60 * 60 * 1000);
      const conflict = await rescheduleLesson(blockingStartsAt);
      assert.equal(conflict.status, 409, 'a move cannot overlap another teacher lesson');
      assert.equal((await conflict.json()).code, 'Lesson.TeacherConflict');
      const scheduleRange = new URLSearchParams({ from: scheduledAt(1).toISOString(), to: scheduledAt(7).toISOString() });
      const lessonsResponse = await request(`${lessonPath}?${scheduleRange}`, teacherToken);
      assert.equal(lessonsResponse.status, 200, await lessonsResponse.clone().text());
      const currentLessons = await lessonsResponse.json();
      assert.ok(Array.isArray(currentLessons), `expected lesson list, got ${JSON.stringify(currentLessons)}`);
      assert.equal(new Date(currentLessons.find(lesson => lesson.id === movableLessonId).startsAt).getTime(), movedStartsAt.getTime(),
        'a rejected move leaves the last saved time intact');

      const capacityStudentId = 'cc17a5ed-ef16-43f5-b869-d8003955a9ae';
      assert.equal((await request(`/api/course-management/v1/workspaces/${workspaceId}/students`, teacherToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: capacityStudentId, displayName: 'Derslik öğrencisi', email: 'room@example.test', phoneNumber: null })
      })).status, 201);
      assert.equal((await request(`${groupPath}/${capacityStudentId}`, teacherToken, { method: 'POST' })).status, 201);
      const branchId = '7d18f280-326e-4e5c-a8e5-1209e5c1f93a';
      const roomId = '95300662-e3e0-44ea-9f0e-f4c09c4e14a8';
      const branchPath = `/api/course-management/v1/workspaces/${workspaceId}/branches`;
      assert.equal((await request(branchPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: branchId, name: 'Merkez' }) })).status, 201);
      const roomPath = `${branchPath}/${branchId}/rooms`;
      assert.equal((await request(roomPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: roomId, name: 'Küçük sınıf', capacity: 1 }) })).status, 201);
      const createSession = (id, startsAt, teacherUserId = subject, studentId = null, selectedGroupId = null, selectedRoomId = null) =>
        request(lessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ id, courseId, groupId: selectedGroupId, studentId, teacherUserId,
            startsAt: startsAt.toISOString(), endsAt: new Date(startsAt.getTime() + 60 * 60 * 1000).toISOString(), roomId: selectedRoomId }) });
      const capacityResponse = await createSession('cab9d3d3-f7cc-4464-b9ef-148f8714a3f4', scheduledAt(20), subject, null, groupId, roomId);
      assert.equal(capacityResponse.status, 409);
      assert.equal((await capacityResponse.json()).code, 'Room.CapacityExceeded', 'room capacity is checked against active group participants');

      const assistantTeacherId = 'eb708822-8e3d-4c61-b08c-d11c769d43fd';
      const assistantInvitationId = '6673284c-c272-4b4a-a49e-917aeb60ba26';
      const assistantInvitation = await request(invitePath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: assistantInvitationId, targetUserId: assistantTeacherId, role: 2 }) });
      assert.equal(assistantInvitation.status, 201);
      const assistantToken = token('course-management', assistantTeacherId, secret, 'Teacher');
      const assistantAccept = await request(`/api/course-management/v1/participation/invitations/${assistantInvitationId}/accept`, assistantToken, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'İkinci öğretmen' }) });
      assert.equal(assistantAccept.status, 200);
      assert.equal((await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, assistantToken)).status, 404,
        'audit entries remain private to the workspace owner');

      const sharedRoomStart = scheduledAt(30);
      const roomBookingId = '7086f74f-142c-4d11-872b-89a68d58bb20';
      assert.equal((await createSession(roomBookingId, sharedRoomStart, subject, linkedStudentId, null, roomId)).status, 201);
      const roomConflict = await createSession('e4d29b97-a272-47d8-99c3-f0d75632ef5b', sharedRoomStart,
        assistantTeacherId, capacityStudentId, null, roomId);
      assert.equal(roomConflict.status, 409, await roomConflict.clone().text());
      assert.equal((await roomConflict.json()).code, 'Room.Conflict', 'room conflicts are checked after teacher and student conflicts');
      const studentConflict = await createSession('715ee8a8-a0ef-4e9a-847d-fb0440868ec2', sharedRoomStart,
        assistantTeacherId, linkedStudentId);
      assert.equal(studentConflict.status, 409);
      assert.equal((await studentConflict.json()).code, 'Lesson.StudentConflict');

      const seriesId = 'dc35e85c-8f96-4d80-aebd-46cc953ddfca';
      const seriesStarts = new Date(Date.now() + 50 * 24 * 60 * 60 * 1000);
      seriesStarts.setUTCHours(0, 0, 0, 0);
      const seriesEnds = new Date(seriesStarts.getTime() + 14 * 24 * 60 * 60 * 1000);
      const seriesWeekdayMask = 1 << ((seriesStarts.getUTCDay() + 6) % 7);
      const seriesPath = `/api/course-management/v1/workspaces/${workspaceId}/lesson-series`;
      const createSeries = (id, startsOn, endsOn, startTime, timeZoneId = 'Europe/Istanbul', daysOfWeekMask = seriesWeekdayMask) =>
        request(seriesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ id, courseId, groupId, studentId: null, teacherUserId: subject, timeZoneId,
            startsOn, endsOn, startTime, durationMinutes: 60, daysOfWeekMask, roomId: null }) });
      const seriesDate = seriesStarts.toISOString().slice(0, 10);
      const seriesEndDate = seriesEnds.toISOString().slice(0, 10);
      const createdSeries = await createSeries(seriesId, seriesDate, seriesEndDate, '15:00');
      assert.equal(createdSeries.status, 200, await createdSeries.clone().text());
      assert.ok((await createdSeries.json()).sessionsCreated >= 2);
      const seriesRange = new URLSearchParams({ from: seriesStarts.toISOString(), to: new Date(seriesEnds.getTime() + 24 * 60 * 60 * 1000).toISOString() });
      const seriesLessons = await (await request(`${lessonPath}?${seriesRange}`, teacherToken)).json();
      const firstLessonPage = await request(`${lessonPath}?${seriesRange}&skip=0&take=1`, teacherToken);
      assert.equal(firstLessonPage.status, 200);
      assert.equal((await firstLessonPage.json()).length, 1, 'lesson lists return a requested page');
      assert.equal((await request(`${lessonPath}?${seriesRange}&take=501`, teacherToken)).status, 400,
        'lesson page size cannot exceed the endpoint limit');
      const activeSeriesLessons = seriesLessons.filter(lesson => lesson.seriesId === seriesId && lesson.status === 'Scheduled');
      assert.ok(activeSeriesLessons.length >= 2);
      const singleMoveForSeries = await request(`${lessonPath}/${activeSeriesLessons[0].id}/schedule`, teacherToken, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ startsAt: scheduledAt(55).toISOString(), endsAt: new Date(scheduledAt(55).getTime() + 3600000).toISOString(), roomId: null }) });
      assert.equal(singleMoveForSeries.status, 409);
      assert.equal((await singleMoveForSeries.json()).code, 'Lesson.SeriesOccurrenceMustUseSeriesEditor');
      const updateSeries = await request(`${seriesPath}/${seriesId}/schedule`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ endsOn: seriesEndDate, startTime: '16:00', durationMinutes: 60, daysOfWeekMask: seriesWeekdayMask, roomId: null }) });
      assert.equal(updateSeries.status, 200, await updateSeries.clone().text());
      const updateSeriesResult = await updateSeries.json();
      assert.ok(updateSeriesResult.sessionsCreated >= 1 && updateSeriesResult.sessionsCancelled >= 1,
        'changing a recurring time cancels the old occurrences and creates replacements');
      const replacementSeriesLessons = (await (await request(`${lessonPath}?${seriesRange}`, teacherToken)).json())
        .filter(lesson => lesson.seriesId === seriesId && lesson.status === 'Scheduled');
      const cancelledSeries = await request(`${seriesPath}/${seriesId}/cancel-future`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: '{}' });
      assert.equal(cancelledSeries.status, 200);
      assert.ok((await cancelledSeries.json()).sessionsCancelled >= 1);
      const seriesCancellationCredits = (await (await request(staffCreditPath, teacherToken)).json())
        .filter(credit => replacementSeriesLessons.some(lesson => lesson.id === credit.sourceLessonId));
      assert.equal(seriesCancellationCredits.length, replacementSeriesLessons.length * 2,
        'cancelling a group series grants each active participant one credit per cancelled session');

      const invalidDstSeries = await createSeries('16ecdcf4-79ba-4bd8-8f5d-a564a51db7cf', '2027-03-14', '2027-03-14', '02:30', 'America/New_York', 64);
      assert.equal(invalidDstSeries.status, 400);
      assert.equal((await invalidDstSeries.json()).code, 'LessonSeries.InvalidLocalTime', 'nonexistent DST wall-clock times are rejected');
      const ambiguousDstSeries = await createSeries('82582e53-6418-41be-bf88-8595f9c28795', '2027-11-07', '2027-11-07', '01:30', 'America/New_York', 64);
      assert.equal(ambiguousDstSeries.status, 400);
      assert.equal((await ambiguousDstSeries.json()).code, 'LessonSeries.InvalidLocalTime', 'ambiguous DST wall-clock times are rejected');

      const attendanceLessonId = 'f0e46f1e-8af6-44bd-bab2-d15abfed22a8';
      assert.equal((await createLesson(attendanceLessonId, scheduledAt(40))).status, 201);
      const movedIntoPast = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "LessonSessions" SET "StartsAt" = NOW() - INTERVAL '2 hours', "EndsAt" = NOW() - INTERVAL '1 hour' WHERE "Id" = '${attendanceLessonId}';`, encoding: 'utf8'
      });
      assert.equal(movedIntoPast.status, 0, movedIntoPast.stderr);
      const attendancePath = studentId => `${lessonPath}/${attendanceLessonId}/students/${studentId}/attendance`;
      const markAttendance = (studentId, status) => request(attendancePath(studentId), teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) });
      assert.equal((await markAttendance(linkedStudentId, 1)).status, 200);
      assert.equal((await markAttendance(linkedStudentId, 1)).status, 200, 'repeating the same attendance is idempotent');
      const changedAttendance = await markAttendance(linkedStudentId, 2);
      assert.equal(changedAttendance.status, 200);
      assert.equal((await changedAttendance.json()).status, 'Absent');
      assert.equal((await markAttendance(capacityStudentId, 3)).status, 200);
      const completedLesson = await request(`${lessonPath}/${attendanceLessonId}/complete`, teacherToken, { method: 'POST' });
      assert.equal(completedLesson.status, 204, 'a past lesson completes after every participant has attendance');
      const studentAttendance = await request('/api/course-management/v1/me/attendance', courseStudent);
      assert.equal(studentAttendance.status, 200, await studentAttendance.clone().text());
      assert.equal((await studentAttendance.json()).find(item => item.lessonId === attendanceLessonId)?.status, 'Absent',
        'student attendance history returns only the linked student records');
      const lockedAttendance = await markAttendance(capacityStudentId, 1);
      assert.equal(lockedAttendance.status, 409);
      assert.equal((await lockedAttendance.json()).code, 'Attendance.LessonClosed');
      assert.equal((await (await request(staffCreditPath, teacherToken)).json())
        .filter(credit => credit.sourceLessonId === attendanceLessonId).length, 0, 'no-show does not generate a make-up credit');
      const cancelledLessonId = 'fd283ad7-d2a0-490e-82d4-805f416a7f57';
      assert.equal((await createLesson(cancelledLessonId, scheduledAt(45))).status, 201);
      const cancelLessonPath = `${lessonPath}/${cancelledLessonId}/cancel`;
      assert.equal((await request(cancelLessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })).status, 204);
      assert.ok(readReminders(cancelledLessonId).every(item => item.status === 2),
        'teacher cancellation cancels each active participant reminder');
      const repeatedCancel = await request(cancelLessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
      assert.equal(repeatedCancel.status, 409);
      assert.equal((await repeatedCancel.json()).code, 'Lesson.NotScheduled');

      const groupCancelledLessonId = '21fe1903-30a0-4a6b-a3d5-d1bce8790eb5';
      assert.equal((await createLesson(groupCancelledLessonId, scheduledAt(47))).status, 201);
      assert.equal((await request(`${lessonPath}/${groupCancelledLessonId}/cancel`, teacherToken, { method: 'POST' })).status, 204);
      const groupCredits = await (await request(staffCreditPath, teacherToken)).json();
      const sourceCredits = groupCredits.filter(credit => credit.sourceLessonId === groupCancelledLessonId);
      assert.equal(sourceCredits.length, 2, 'teacher cancellation grants each active group participant one credit');
      assert.ok(sourceCredits.every(credit => credit.status === 'Available'), 'teacher cancellation credits are immediately available');
      assert.ok(sourceCredits.every(credit => new Date(credit.expiresAt).getTime() - new Date(credit.grantedAt).getTime() === 30 * 86400000));
      const expireCredit = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "MakeupCredits" SET "ExpiresAt" = NOW() - INTERVAL '1 minute' WHERE "Id" = '${sourceCredits[0].id}';`, encoding: 'utf8'
      });
      assert.equal(expireCredit.status, 0, expireCredit.stderr);
      const expiredCredit = (await (await request(staffCreditPath, teacherToken)).json()).find(credit => credit.id === sourceCredits[0].id);
      assert.equal(expiredCredit.status, 'Expired');
      const approveExpiredCredit = await request(`${staffCreditPath}/${sourceCredits[0].id}/approve`, teacherToken, { method: 'POST' });
      assert.equal(approveExpiredCredit.status, 409);
      assert.equal((await approveExpiredCredit.json()).code, 'MakeupCredit.Expired');

      const earlyStudentCancelId = 'a0c8f94b-7902-4eb7-8a13-5e443a595cc9';
      const earlyStudentStart = new Date(Date.now() + 48 * 60 * 60 * 1000);
      assert.equal((await createSession(earlyStudentCancelId, earlyStudentStart, subject, null, groupId)).status, 201);
      const studentCancelPath = `/api/course-management/v1/me/lessons/${earlyStudentCancelId}/cancel-participation`;
      const earlyStudentCancel = await request(studentCancelPath, courseStudent, { method: 'POST' });
      assert.equal(earlyStudentCancel.status, 200, await earlyStudentCancel.clone().text());
      const earlyStudentReminders = readReminders(earlyStudentCancelId);
      assert.equal(earlyStudentReminders.length, 1);
      assert.equal(earlyStudentReminders[0].recipientUserId, otherUser);
      assert.equal(earlyStudentReminders[0].status, 2, 'student cancellation cancels only that student’s pending lesson reminder');
      const earlyCredit = await earlyStudentCancel.json();
      assert.equal(earlyCredit.creditStatus, 'PendingTeacherApproval');
      const repeatedStudentCancel = await request(studentCancelPath, courseStudent, { method: 'POST' });
      assert.equal((await repeatedStudentCancel.json()).creditId, earlyCredit.creditId, 'retry does not issue another credit');
      const upcomingStudentLessons = await (await request('/api/course-management/v1/me/lessons', courseStudent)).json();
      assert.ok(!upcomingStudentLessons.some(lesson => lesson.id === earlyStudentCancelId), 'student panel removes cancelled group participation');
      const lessonRoster = await (await request(`${lessonPath}?${scheduleRange}`, teacherToken)).json();
      const cancelledGroupParticipant = lessonRoster.find(lesson => lesson.id === earlyStudentCancelId).students
        .find(student => student.studentId === linkedStudentId);
      assert.equal(cancelledGroupParticipant.isActive, false, 'the group lesson keeps an auditable inactive participant snapshot');
      assert.equal(lessonRoster.find(lesson => lesson.id === earlyStudentCancelId).students
        .find(student => student.studentId === capacityStudentId).isActive, true, 'other group participants remain active');
      const ownCredits = await (await request(studentCreditPath, courseStudent)).json();
      assert.ok(ownCredits.some(credit => credit.id === earlyCredit.creditId));
      assert.ok(!(await (await request(studentCreditPath, token('course-management', assistantTeacherId, secret, 'Student'))).json())
        .some(credit => credit.id === earlyCredit.creditId), 'students only see their own credits');

      const lateStudentCancelId = '453eead1-ccbe-4146-b331-b0908695b3bb';
      const lateStudentStart = new Date(Date.now() + 23 * 60 * 60 * 1000);
      assert.equal((await createSession(lateStudentCancelId, lateStudentStart, subject, linkedStudentId)).status, 201);
      const lateStudentCancel = await request(`/api/course-management/v1/me/lessons/${lateStudentCancelId}/cancel-participation`, courseStudent, { method: 'POST' });
      assert.equal(lateStudentCancel.status, 200);
      assert.equal((await lateStudentCancel.json()).creditStatus, null, 'cancellation under 24 hours creates no credit');

      const approveCreditPath = `${staffCreditPath}/${earlyCredit.creditId}/approve`;
      const approvedCredit = await request(approveCreditPath, teacherToken, { method: 'POST' });
      assert.equal(approvedCredit.status, 200);
      assert.equal((await approvedCredit.json()).status, 'Available');
      const makeupLessonId = 'e1e54159-6995-4b20-8c9b-4abfa515a80e';
      const makeupStartsAt = scheduledAt(10);
      const createMakeupLesson = (id, startsAt) => request(lessonPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id, courseId, groupId: null,
          studentId: linkedStudentId, teacherUserId: subject, startsAt: startsAt.toISOString(),
          endsAt: new Date(startsAt.getTime() + 60 * 60 * 1000).toISOString(), roomId: null, makeupCreditId: earlyCredit.creditId }) });
      const plannedMakeup = await createMakeupLesson(makeupLessonId, makeupStartsAt);
      assert.equal(plannedMakeup.status, 201, await plannedMakeup.clone().text());
      assert.equal((await (await request(staffCreditPath, teacherToken)).json()).find(credit => credit.id === earlyCredit.creditId).status, 'Scheduled');
      assert.equal((await request(`${lessonPath}/${makeupLessonId}/cancel`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await (await request(staffCreditPath, teacherToken)).json()).find(credit => credit.id === earlyCredit.creditId).status, 'Available',
        'teacher cancellation of a planned make-up restores the original credit without duplicating it');
      const replacementMakeupId = '17e51a1e-8169-497c-8db1-3e5cae3ec843';
      assert.equal((await createMakeupLesson(replacementMakeupId, scheduledAt(11))).status, 201);
      const unauthorizedApproval = await request(approveCreditPath, token('course-management', assistantTeacherId, secret, 'Teacher'), { method: 'POST' });
      assert.equal(unauthorizedApproval.status, 404, 'a different teacher cannot manage an unassigned student cancellation credit');
      const makeupBecamePast = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "LessonSessions" SET "StartsAt" = NOW() - INTERVAL '2 hours', "EndsAt" = NOW() - INTERVAL '1 hour' WHERE "Id" = '${replacementMakeupId}';`, encoding: 'utf8'
      });
      assert.equal(makeupBecamePast.status, 0, makeupBecamePast.stderr);
      assert.equal((await request(`${lessonPath}/${replacementMakeupId}/students/${linkedStudentId}/attendance`, teacherToken, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: 1 }) })).status, 200);
      assert.equal((await request(`${lessonPath}/${replacementMakeupId}/complete`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await (await request(staffCreditPath, teacherToken)).json()).find(credit => credit.id === earlyCredit.creditId).status, 'Consumed');

      const financePath = `/api/course-management/v1/workspaces/${workspaceId}/finance`;
      assert.equal((await request(`${financePath}/students/${linkedStudentId}/account`, courseStudent)).status, 404,
        'students cannot read staff financial records');
      assert.equal((await request(`${financePath}/students/${linkedStudentId}/account`, token('course-management', assistantTeacherId, secret, 'Teacher'))).status, 404,
        'non-owner teachers cannot read workspace finances');
      assert.equal((await request(`${financePath}/summary`, token('course-management', assistantTeacherId, secret, 'Teacher'))).status, 404,
        'non-owner teachers cannot read workspace finance summaries');
      assert.equal((await request(`${financePath}/report`, token('course-management', assistantTeacherId, secret, 'Teacher'))).status, 404,
        'non-owner teachers cannot read workspace finance reports');
      const financeCoursesPath = `/api/course-management/v1/workspaces/${workspaceId}/courses`;
      const termCourseId = 'e53c2157-1c33-4932-8ff4-2b31f4a5d635';
      assert.equal((await request(financeCoursesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: termCourseId, name: 'Dönem programı', subject: 'Matematik' }) })).status, 201);
      const feeAgreementsPath = `${financePath}/fee-agreements`;
      const termAgreementId = 'c24905b6-736a-4e31-84c2-d4b6b7c34cd9';
      const termAgreementRequest = { id: termAgreementId, studentId: linkedStudentId, courseId: termCourseId, model: 1, amount: 1000 };
      const createTermAgreement = () => request(feeAgreementsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(termAgreementRequest) });
      assert.equal((await createTermAgreement()).status, 201);
      assert.equal((await createTermAgreement()).status, 200, 'agreement retries are idempotent');
      const termInstallmentsPath = `${feeAgreementsPath}/${termAgreementId}/installments`;
      const firstInstallmentId = 'e81c0327-6027-40b1-9122-dc9eb3f445f3';
      const secondInstallmentId = '0c13d873-336a-4e0c-a53c-c3891cc4ccfa';
      const createInstallment = (id, amount, dueOn) => request(termInstallmentsPath, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id, label: 'Taksit', amount, dueOn }) });
      // Dates are relative to today: a fixed due date makes the "3 days before" reminder fall into the past and the
      // scheduler (correctly) skips it, which broke this test from 29.10.2026 06:00 UTC.
      const firstDueOn = utcDayOffset(40);
      assert.equal((await createInstallment(firstInstallmentId, 600, firstDueOn)).status, 201);
      assert.equal((await createInstallment(secondInstallmentId, 400, utcDayOffset(70))).status, 201);
      const firstInstallmentReminders = readReminders(firstInstallmentId);
      assert.equal(firstInstallmentReminders.length, 2);
      assert.deepEqual(firstInstallmentReminders.map(item => item.type).sort(), [3, 4]);
      assert.ok(firstInstallmentReminders.every(item => item.status === 1));
      assert.ok(firstInstallmentReminders.every(item => Date.parse(item.targetAt) === Date.parse(`${firstDueOn}T06:00:00.000Z`)),
        'date-only installment reminders use the Europe/Istanbul 09:00 due-day anchor');
      assert.equal(Date.parse(firstInstallmentReminders.find(item => item.type === 3).notifyAt), Date.parse(`${utcDayOffset(37)}T06:00:00.000Z`));
      assert.equal(Date.parse(firstInstallmentReminders.find(item => item.type === 4).notifyAt), Date.parse(`${firstDueOn}T06:00:00.000Z`));
      const paymentId = '13acc6cd-4e09-46e3-b434-b17d08b50f22';
      const paymentRequest = { id: paymentId, studentId: linkedStudentId, method: 1, amount: 650,
        receivedAt: new Date().toISOString(), note: 'Kısmi tahsilat', allocations: [
          { installmentId: firstInstallmentId, amount: 500 }, { installmentId: secondInstallmentId, amount: 150 }] };
      const recordPayment = body => request(`${financePath}/payments`, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body) });
      const recordedPayment = await recordPayment(paymentRequest);
      assert.equal(recordedPayment.status, 201, await recordedPayment.clone().text());
      assert.equal((await recordPayment(paymentRequest)).status, 200, 'payment retries do not create a second collection');
      const conflictingPayment = await recordPayment({ ...paymentRequest, amount: 700 });
      assert.equal(conflictingPayment.status, 409, 'an idempotency key cannot be reused with different payment data');
      const overAllocation = await recordPayment({ ...paymentRequest, id: '9dd9d790-bc56-4694-b451-a394e532a161', amount: 150,
        allocations: [{ installmentId: firstInstallmentId, amount: 101 }] });
      assert.equal(overAllocation.status, 409, 'a payment cannot allocate beyond the remaining installment balance');
      assert.equal((await recordPayment({ ...paymentRequest, id: '41f38e3f-4cde-497d-9e7d-12025ff76fac', amount: 0.001,
        allocations: [] })).status, 400, 'money is limited to two decimal places');
      const termAccountPath = `${financePath}/students/${linkedStudentId}/account`;
      const partialAccount = await (await request(termAccountPath, teacherToken)).json();
      assert.equal(partialAccount.balance, 350);
      assert.deepEqual(partialAccount.installments.map(item => item.remainingAmount), [100, 250]);
      const reversePaymentId = '382d93be-221b-4535-a4ad-3e24560d4a42';
      const reversePaymentPath = `${financePath}/payments/${paymentId}/reverse`;
      const reversePaymentRequest = { id: reversePaymentId, receivedAt: new Date().toISOString(), reason: 'Yanlış tahsilat' };
      const reversePayment = () => request(reversePaymentPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(reversePaymentRequest) });
      assert.equal((await reversePayment()).status, 201);
      assert.equal((await reversePayment()).status, 200, 'reversing the same payment is idempotent');
      assert.equal((await request(reversePaymentPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...reversePaymentRequest, reason: 'Farklı neden' }) })).status, 409,
        'a reversal key cannot be replayed with a different reason');
      const reversedAccount = await (await request(termAccountPath, teacherToken)).json();
      assert.equal(reversedAccount.balance, 1000, 'a reverse entry restores the original receivable');
      assert.deepEqual(reversedAccount.installments.map(item => item.remainingAmount), [600, 400]);
      const fullySettledPaymentId = '98736470-cd53-413d-9130-94563b0e7a0b';
      const fullySettledPayment = await recordPayment({ id: fullySettledPaymentId, studentId: linkedStudentId, method: 1,
        amount: 600, receivedAt: new Date().toISOString(), note: null,
        allocations: [{ installmentId: firstInstallmentId, amount: 600 }] });
      assert.equal(fullySettledPayment.status, 201, await fullySettledPayment.clone().text());
      assert.ok(readReminders(firstInstallmentId).every(item => item.status === 2),
        'fully paid installment cancels both pending financial reminders');
      const fullySettledReversal = await request(`${financePath}/payments/${fullySettledPaymentId}/reverse`, teacherToken,
        { method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ id: 'd3e6c0d2-5531-41d8-a837-b5102ddbf563', reason: 'Düzeltme' }) });
      assert.equal(fullySettledReversal.status, 201, await fullySettledReversal.clone().text());
      const reopenedInstallmentReminders = readReminders(firstInstallmentId);
      assert.equal(reopenedInstallmentReminders.filter(item => item.status === 1).length, 2,
        'a payment reversal restores future reminders for the reopened installment');
      const reverseChargeId = '412ec38e-1b3f-4d58-a6c5-45af4068c665';
      const reverseChargePath = `${financePath}/installments/${firstInstallmentId}/reverse`;
      const reverseCharge = () => request(reverseChargePath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: reverseChargeId }) });
      assert.equal((await reverseCharge()).status, 201);
      assert.equal((await reverseCharge()).status, 200, 'charge correction is recorded once as a reverse entry');
      assert.equal((await (await request(termAccountPath, teacherToken)).json()).balance, 400);

      const monthlyCourseId = '34c68f84-0656-4563-9cf8-71ce14a2840b';
      assert.equal((await request(financeCoursesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: monthlyCourseId, name: 'Aylık program', subject: 'Matematik' }) })).status, 201);
      const monthlyAgreementId = '1b932fcc-2d13-4b8a-a1a0-e945bfa8bd66';
      const monthlyAgreement = await request(feeAgreementsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: monthlyAgreementId, studentId: linkedStudentId, courseId: monthlyCourseId, model: 2, amount: 1200 }) });
      assert.equal(monthlyAgreement.status, 201);
      const monthlyAccountPath = `${financePath}/students/${linkedStudentId}/account`;
      const beforeMonthlyInstallment = await (await request(monthlyAccountPath, teacherToken)).json();
      assert.deepEqual(beforeMonthlyInstallment.installments.filter(item => item.feeAgreementId === monthlyAgreementId), [],
        'monthly plans do not create prorated or automatic charges');
      assert.equal((await request(`${feeAgreementsPath}/${monthlyAgreementId}/installments`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: 'd6c01777-11b8-4f21-a4c8-504e64ccbc33',
          label: 'Kasım', amount: 1200, dueOn: utcDayOffset(45) }) })).status, 201);
      const afterMonthlyInstallment = await (await request(monthlyAccountPath, teacherToken)).json();
      assert.equal(afterMonthlyInstallment.installments.filter(item => item.feeAgreementId === monthlyAgreementId).length, 1);
      assert.equal(afterMonthlyInstallment.balance, 1600);
      assert.equal((await request(`${feeAgreementsPath}/${monthlyAgreementId}/installments`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: '2ff798be-b035-4b33-bc01-676d9eb84910',
          label: 'Aralık', amount: 1200, dueOn: utcDayOffset(75) }) })).status, 201);
      const twoMonthsAccount = await (await request(monthlyAccountPath, teacherToken)).json();
      assert.deepEqual(twoMonthsAccount.installments.filter(item => item.feeAgreementId === monthlyAgreementId)
        .map(item => item.dueOn), [utcDayOffset(45), utcDayOffset(75)]);
      assert.equal(twoMonthsAccount.balance, 2800, 'the owner sets each full monthly amount and due date; no partial-month proration is applied');

      const hourlyCourseId = '655260df-c630-4450-b96d-8c0dbeb29d78';
      assert.equal((await request(financeCoursesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: hourlyCourseId, name: 'Saatlik ders', subject: 'İngilizce' }) })).status, 201);
      const hourlyAgreementId = 'eed2bb50-044e-42fa-963b-3f588c9d8025';
      assert.equal((await request(feeAgreementsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: hourlyAgreementId, studentId: linkedStudentId, courseId: hourlyCourseId,
          model: 3, amount: 600, billingUnit: 2 }) })).status, 201);
      const hourlyLessonId = '20174601-f009-4e13-b60d-bf70f4c538aa';
      const hourlyStartsAt = scheduledAt(100);
      assert.equal((await request(lessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: hourlyLessonId, courseId: hourlyCourseId, groupId: null, studentId: linkedStudentId,
          teacherUserId: subject, startsAt: hourlyStartsAt.toISOString(), endsAt: new Date(hourlyStartsAt.getTime() + 45 * 60000).toISOString() }) })).status, 201);
      const hourlyPast = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "LessonSessions" SET "StartsAt" = NOW() - INTERVAL '90 minutes', "EndsAt" = NOW() - INTERVAL '45 minutes' WHERE "Id" = '${hourlyLessonId}';`, encoding: 'utf8'
      });
      assert.equal(hourlyPast.status, 0, hourlyPast.stderr);
      assert.equal((await request(`${lessonPath}/${hourlyLessonId}/students/${linkedStudentId}/attendance`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: 2 }) })).status, 200,
      'a completed lesson can record the student as absent');
      assert.equal((await request(`${lessonPath}/${hourlyLessonId}/complete`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await request(`${lessonPath}/${hourlyLessonId}/complete`, teacherToken, { method: 'POST' })).status, 409);
      const hourlyAccount = await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json();
      assert.equal(hourlyAccount.installments.find(item => item.sourceLessonId === hourlyLessonId).amount, 450,
        'per-hour lesson billing rounds a 45-minute lesson to two decimal places');
      assert.equal(hourlyAccount.installments.filter(item => item.sourceLessonId === hourlyLessonId).length, 1);

      const perLessonCourseId = '1c6fe2df-66a7-45fc-84a0-54a83f4e01e7';
      assert.equal((await request(financeCoursesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: perLessonCourseId, name: 'Ders başı ücret', subject: 'Türkçe' }) })).status, 201);
      const perLessonAgreementId = '79455f2b-f57b-4e36-9e14-1f6c73e927b9';
      assert.equal((await request(feeAgreementsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: perLessonAgreementId, studentId: linkedStudentId, courseId: perLessonCourseId,
          model: 3, amount: 350, billingUnit: 1 }) })).status, 201);
      const perLessonId = 'e6bf4b3e-3c50-4075-b0df-5415847ca10d';
      const perLessonStartsAt = scheduledAt(105);
      assert.equal((await request(lessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: perLessonId, courseId: perLessonCourseId, groupId: null, studentId: linkedStudentId,
          teacherUserId: subject, startsAt: perLessonStartsAt.toISOString(), endsAt: new Date(perLessonStartsAt.getTime() + 60 * 60000).toISOString() }) })).status, 201);
      const perLessonPast = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "LessonSessions" SET "StartsAt" = NOW() - INTERVAL '2 hours', "EndsAt" = NOW() - INTERVAL '1 hour' WHERE "Id" = '${perLessonId}';`, encoding: 'utf8'
      });
      assert.equal(perLessonPast.status, 0, perLessonPast.stderr);
      assert.equal((await request(`${lessonPath}/${perLessonId}/students/${linkedStudentId}/attendance`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: 1 }) })).status, 200);
      assert.equal((await request(`${lessonPath}/${perLessonId}/complete`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json())
        .installments.find(item => item.sourceLessonId === perLessonId).amount, 350, 'per-lesson model charges one agreed unit on completion');

      const packageCourseId = '239b3c04-957b-439d-9b90-2b2e27b9ff2d';
      assert.equal((await request(financeCoursesPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: packageCourseId, name: 'Ders paketi', subject: 'İngilizce' }) })).status, 201);
      const packageAgreementId = '92d782f0-0aee-43e6-b884-721cc74bc3bc';
      assert.equal((await request(feeAgreementsPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: packageAgreementId, studentId: linkedStudentId, courseId: packageCourseId, model: 4,
          amount: 1500, packageSessions: 5, packageExpiresAt: new Date(Date.now() + 365 * 86400000).toISOString() }) })).status, 201);
      assert.equal((await request(`${feeAgreementsPath}/${packageAgreementId}/installments`, teacherToken, { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: 'cd9507f6-339d-40a0-a03e-690e0ffba862',
          label: '5 ders paketi', amount: 1500, dueOn: utcDayOffset(50) }) })).status, 201);
      const packageLessonId = '3d32939d-1f5b-48ba-9d08-7652ab57a323';
      const packageStartsAt = scheduledAt(110);
      assert.equal((await request(lessonPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: packageLessonId, courseId: packageCourseId, groupId: null, studentId: linkedStudentId,
          teacherUserId: subject, startsAt: packageStartsAt.toISOString(), endsAt: new Date(packageStartsAt.getTime() + 3600000).toISOString() }) })).status, 201);
      const packagePast = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "LessonSessions" SET "StartsAt" = NOW() - INTERVAL '2 hours', "EndsAt" = NOW() - INTERVAL '1 hour' WHERE "Id" = '${packageLessonId}';`, encoding: 'utf8'
      });
      assert.equal(packagePast.status, 0, packagePast.stderr);
      assert.equal((await request(`${lessonPath}/${packageLessonId}/students/${linkedStudentId}/attendance`, teacherToken, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: 2 }) })).status, 200,
      'a completed package lesson can record the student as absent');
      assert.equal((await request(`${lessonPath}/${packageLessonId}/complete`, teacherToken, { method: 'POST' })).status, 204);
      const packageAccount = await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json();
      assert.equal(packageAccount.packageBalances.find(item => item.agreementId === packageAgreementId).remainingSessions, 4,
        'completing a lesson consumes the package entitlement even when the student is absent');
      const consumedMovement = packageAccount.packageMovements.find(item => item.lessonSessionId === packageLessonId);
      assert.ok(consumedMovement);
      const reverseMovementId = '1d1e5d89-a3b5-4b9d-9574-40e4fa02c591';
      const reverseMovementPath = `${financePath}/package-movements/${consumedMovement.id}/reverse`;
      const reverseMovement = () => request(reverseMovementPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: reverseMovementId }) });
      assert.equal((await reverseMovement()).status, 201, 'an owner can restore an incorrectly consumed package right with a ledger entry');
      assert.equal((await reverseMovement()).status, 200, 'package movement reversals are idempotent');
      assert.equal((await request(reverseMovementPath, token('course-management', assistantTeacherId, secret, 'Teacher'), { method: 'POST',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: 'a0aeb486-2d56-4a77-a38b-c0cc33989f72' }) })).status, 404,
      'non-owner teachers cannot change package rights');
      assert.equal((await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json())
        .packageBalances.find(item => item.agreementId === packageAgreementId).remainingSessions, 5,
      'restored rights remain visible in the package ledger');
      assert.equal((await request(`${lessonPath}/${packageLessonId}/complete`, teacherToken, { method: 'POST' })).status, 409);
      assert.equal((await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json())
        .packageBalances.find(item => item.agreementId === packageAgreementId).remainingSessions, 5,
        'retrying completion cannot consume the package twice');
      const ownFinanceResponse = await request('/api/course-management/v1/me/finance', courseStudent);
      assert.equal(ownFinanceResponse.status, 200, await ownFinanceResponse.clone().text());
      const ownFinanceSummaries = await ownFinanceResponse.json();
      assert.equal(ownFinanceSummaries.length, 1, 'student finance excludes unlinked and other students in the same workspace');
      const ownFinance = ownFinanceSummaries.find(item => item.workspaceId === workspaceId);
      assert.ok(ownFinance);
      assert.equal(ownFinance.balance, 5100, 'students can read their own current balance');
      assert.equal(ownFinance.packageBalances.find(item => item.agreementId === packageAgreementId).remainingSessions, 5);
      assert.equal(Object.hasOwn(ownFinance, 'payments'), false, 'student finance omits payment history');
      assert.equal(Object.hasOwn(ownFinance, 'installments'), false, 'student finance omits installment details');
      assert.deepEqual(Object.keys(ownFinance).sort(), ['balance', 'packageBalances', 'workspaceId', 'workspaceName'].sort(),
        'student finance exposes only its workspace label, balance and package rights');
      assert.deepEqual(Object.keys(ownFinance.packageBalances[0]).sort(), ['agreementId', 'expiresAt', 'grantedSessions', 'remainingSessions'].sort(),
        'student package summary exposes only granted, remaining and expiry information');
      const guardianAccount = '9f9f37ee-190f-46b4-a48d-2f1a1e3e51aa';
      const guardianToken = token('course-management', guardianAccount, secret, 'Parent');
      for (const path of ['/api/course-management/v1/me/courses', '/api/course-management/v1/me/lessons',
        '/api/course-management/v1/me/assignments', '/api/course-management/v1/me/finance']) {
        const response = await request(path, guardianToken);
        assert.equal(response.status, 200, `the guardian account's own empty view remains readable: ${path}`);
        assert.deepEqual(await response.json(), [],
          `guardian accounts cannot see a child's records through student self-service: ${path}`);
      }
      assert.equal((await request(`${financePath}/students/${linkedStudentId}/account`, guardianToken)).status, 404,
        'guardian accounts cannot read a child student account directly');

      const pastDueDate = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
      const setPastDue = spawnSync('docker', ['exec', '-i', container, 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'course_test', '-d', 'course_test'], {
        input: `UPDATE "Installments" SET "DueOn" = '${pastDueDate}' WHERE "Id" = '${secondInstallmentId}' OR "SourceLessonId" IN ('${hourlyLessonId}', '${perLessonId}');`, encoding: 'utf8'
      });
      assert.equal(setPastDue.status, 0, setPastDue.stderr);
      const financeSummaryResponse = await request(`${financePath}/summary`, teacherToken);
      assert.equal(financeSummaryResponse.status, 200);
      const financeSummary = await financeSummaryResponse.json();
      assert.equal(financeSummary.totalCharges, 5100);
      assert.equal(financeSummary.totalPayments, 0, 'a payment reversal removes the net cash collection');
      assert.equal(financeSummary.balance, 5100);
      assert.equal(financeSummary.overdueAmount, 1200, 'the summary excludes reversed charges and counts all remaining past-due balances');
      assert.ok(financeSummary.dueInNext30Days >= 1900);
      assert.equal(financeSummary.collectionsByMethod.find(item => item.method === 'Cash').amount, 0,
        'collection totals include same-period reversal entries');
      assert.equal((await request(`${financePath}/summary?from=2026-12-31&to=2026-12-01`, teacherToken)).status, 400,
        'summary rejects an inverted date range');
      const financeReportResponse = await request(`${financePath}/report`, teacherToken);
      assert.equal(financeReportResponse.status, 200, await financeReportResponse.clone().text());
      const financeReport = await financeReportResponse.json();
      const overdueById = new Map(financeReport.overdueInstallments.map(item => [item.installmentId, item.remainingAmount]));
      const reportStudentAccount = await (await request(`${financePath}/students/${linkedStudentId}/account`, teacherToken)).json();
      assert.equal(overdueById.get(secondInstallmentId), 400, 'workspace report lists the unpaid overdue installment balance');
      assert.equal(overdueById.get(hourlyAccount.installments.find(item => item.sourceLessonId === hourlyLessonId).id), 450,
        'workspace report includes the completed hourly lesson charge when overdue');
      assert.equal(overdueById.get(reportStudentAccount.installments.find(item => item.sourceLessonId === perLessonId).id), 350,
      'workspace report includes the completed per-lesson charge when overdue');
      assert.equal([...overdueById.values()].reduce((sum, amount) => sum + amount, 0), 1200);
      assert.equal(financeReport.overdueInstallments.some(item => item.installmentId === firstInstallmentId), false,
        'reversed charges are omitted from the workspace report');
      assert.equal(financeReport.upcomingInstallments.find(item => item.installmentId === 'd6c01777-11b8-4f21-a4c8-504e64ccbc33').remainingAmount, 1200,
        'workspace report includes the complete upcoming monthly installment');
      assert.equal(financeReport.packageBalances.find(item => item.agreementId === packageAgreementId).remainingSessions, 5,
        'workspace report uses the corrected package ledger balance');
      assert.equal(Object.hasOwn(financeReport.overdueInstallments[0], 'email'), false);
      assert.equal(Object.hasOwn(financeReport.overdueInstallments[0], 'phoneNumber'), false);

      const timeOffStartsAt = scheduledAt(80);
      const timeOffId = '89af36b3-366a-48d9-8699-1d196a9c2549';
      const timeOffPath = `/api/course-management/v1/workspaces/${workspaceId}/teacher-time-off`;
      const timeOff = await request(timeOffPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: timeOffId, teacherUserId: subject, startsAt: timeOffStartsAt.toISOString(),
          endsAt: new Date(timeOffStartsAt.getTime() + 3600000).toISOString(), reason: 'Test' }) });
      assert.equal(timeOff.status, 201);
      const timeOffLesson = await createLesson('86081a6f-bd40-468c-9cdc-d29767391905', timeOffStartsAt);
      assert.equal(timeOffLesson.status, 409);
      assert.equal((await timeOffLesson.json()).code, 'Availability.TimeOffConflict');
      assert.equal((await request(`${timeOffPath}/${timeOffId}`, teacherToken, { method: 'DELETE' })).status, 204);

      const availabilityStartsAt = scheduledAt(85);
      availabilityStartsAt.setUTCHours(6, 0, 0, 0);
      const availabilityWeekday = (availabilityStartsAt.getUTCDay() + 6) % 7;
      const availabilityPath = `/api/course-management/v1/workspaces/${workspaceId}/teacher-availability`;
      const availabilityId = '4f0213ee-87c6-4ff5-88f3-99ce0c5d3353';
      const availabilityRequest = { id: availabilityId, teacherUserId: assistantTeacherId, weekday: availabilityWeekday,
        startTime: '09:00', endTime: '17:00', timeZoneId: 'Europe/Istanbul' };
      const createdAvailability = await request(availabilityPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(availabilityRequest) });
      assert.equal(createdAvailability.status, 201);
      const duplicateAvailability = await request(availabilityPath, teacherToken, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(availabilityRequest) });
      assert.equal(duplicateAvailability.status, 409);
      assert.equal((await duplicateAvailability.json()).code, 'Availability.DuplicateWindow');
      const outsideAvailability = await createSession('b1ef52d2-49f5-4e15-b0f8-4279ead86f46', new Date(availabilityStartsAt.getTime() - 3600000), assistantTeacherId, capacityStudentId);
      assert.equal(outsideAvailability.status, 409);
      assert.equal((await outsideAvailability.json()).code, 'Availability.OutsideWorkingHours');
      const insideAvailability = await createSession('cf9a4beb-4522-4cb0-8c7f-0926f32e6f82', new Date(availabilityStartsAt.getTime() + 3600000), assistantTeacherId, capacityStudentId);
      assert.equal(insideAvailability.status, 201, await insideAvailability.clone().text());

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
      const revokedMembershipAudit = await (await request(`/api/course-management/v1/workspaces/${workspaceId}/audit`, teacherToken)).json();
      assert.equal(revokedMembershipAudit.filter(entry => entry.action === 'Membership.Revoked').length, 1);
      const cancelledId = 'a29bc0c7-1b4e-4974-9668-39a6753d20c6';
      assert.equal((await createInvite(teacherToken, cancelledId)).status, 201);
      assert.equal((await request(`${invitePath}/${cancelledId}/cancel`, teacherToken, { method: 'POST' })).status, 204);
      assert.equal((await request(`/api/course-management/v1/participation/invitations/${cancelledId}/accept`, oldStudent, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: 'Öğrenci' })
      })).status, 409);

      const erasedWorkspaceId = 'ef9249b5-b54e-4227-9f41-262d6b06992b';
      const markWorkspaceErased = queryCourseDb(`INSERT INTO "WorkspaceMemberships" ("WorkspaceId", "UserId", "Role", "IsActive")
        VALUES ('${erasedWorkspaceId}', '${subject}', 3, TRUE)
        ON CONFLICT ("WorkspaceId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role", "IsActive" = TRUE;
        UPDATE "Workspaces" SET "IsErasureReadOnly" = TRUE WHERE "Id" = '${erasedWorkspaceId}';`);
      assert.equal(markWorkspaceErased.status, 0, markWorkspaceErased.stderr);
      const erasedWorkspaceView = await request(`/api/course-management/v1/workspaces/${erasedWorkspaceId}`,
        token('course-management', subject, secret, 'Owner'));
      assert.equal(erasedWorkspaceView.status, 200);
      assert.equal((await erasedWorkspaceView.json()).isReadOnly, true,
        'erasure read-only state is visible independently of subscription status');
      const erasedWorkspaceWrite = await request(`/api/course-management/v1/workspaces/${erasedWorkspaceId}/courses`,
        token('course-management', subject, secret, 'Owner'), { method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ id: '8f39195c-5086-45c5-b86d-7e2c1799da25', name: 'Kapalı kurs', subject: 'Matematik' }) });
      assert.equal(erasedWorkspaceWrite.status, 409,
        'erasure read-only state blocks writes even when no subscription exists');
      assert.deepEqual(await erasedWorkspaceWrite.json(), { code: 'Workspace.ErasureReadOnly' });
    }
  } finally {
    if (gatewayChild?.exitCode === null) {
      const exited = new Promise(resolve => gatewayChild.once('exit', resolve));
      gatewayChild.kill();
      await exited;
    }
    if (child.exitCode === null) {
      const exited = new Promise(resolve => child.once('exit', resolve));
      child.kill();
      await exited;
    }
    if (clamServer) await new Promise(resolve => clamServer.close(resolve));
    if (!preserveAttachmentRoot) await rm(attachmentRoot, { recursive: true, force: true });
  }
});

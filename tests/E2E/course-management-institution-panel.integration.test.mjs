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
      // E-mails go out as soon as they are found, checked every second.
      CourseManagement__Email__DelayMinutes: '0', CourseManagement__Email__PollingSeconds: '1',
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

    // E-mails: switched off until the owner turns them on and confirms students and guardians were informed.
    const settingsPath = `${ws}/email-settings`;
    const defaults = await (await call(settingsPath, owner)).json();
    assert.equal(defaults.rules.length, 7);
    assert.ok(defaults.rules.every(rule => rule.enabled === false));
    assert.equal((await call(settingsPath, teacher)).status, 404, 'only the owner manages e-mails');
    const settings = consent => ({ rules: [
      { kind: 2, enabled: true, audience: 1, note: 'TEST Sorularınız için arayın.' },
      { kind: 7, enabled: true, audience: 1, note: null }
    ], installmentDaysBefore: 3, installmentOnDueDay: true, overdueDaysAfter: 3, sendHour: 9,
      replyToEmail: 'kurs@example.test', informedConsentConfirmed: consent });
    const noConsent = await call(settingsPath, owner, 'PUT', settings(false));
    assert.equal(noConsent.status, 400);
    assert.equal((await noConsent.json()).code, 'EmailSettings.ConsentRequired');
    const saved = await call(settingsPath, owner, 'PUT', settings(true));
    assert.equal(saved.status, 200, await saved.clone().text());
    const savedView = await saved.json();
    assert.ok(savedView.informedConsentAt);
    assert.equal(savedView.rules.find(rule => rule.kindValue === 2).note, 'TEST Sorularınız için arayın.');
    const preview = await (await call(`${ws}/email-settings/preview/HomeworkNotDone`, owner)).json();
    assert.equal(preview.institutionName, 'TEST Kurs');
    assert.equal(preview.note, 'TEST Sorularınız için arayın.');

    const guardianEmail = 'veli.mehmet@example.test';
    const guardian = await call(`${ws}/students/${studentB}/guardians`, owner, 'POST', { guardianId: randomUUID(),
      displayName: 'TEST Veli', email: guardianEmail, phoneNumber: null, relationship: 1 });
    assert.equal(guardian.status, 201, await guardian.clone().text());

    const waitFor = async (predicate, what) => {
      for (let attempt = 0; attempt < 60; attempt++) {
        const rows = await (await call(`${ws}/emails`, owner)).json();
        const found = rows.find(predicate);
        if (found) return found;
        await new Promise(done => setTimeout(done, 500));
      }
      assert.fail(`no e-mail record: ${what}`);
    };
    // Mehmet's homework was marked "not done" before e-mails were switched on; it is still recent, so it is told.
    const homework = await waitFor(row => row.kind === 'HomeworkNotDone' && row.studentId === studentB, 'homework');
    assert.equal(homework.status, 'Sent');
    assert.equal(homework.recipients, guardianEmail);
    assert.match(homework.subject, /TEST Mehmet/);
    // Ayşe did hers and has no guardian address: nothing is queued for her.
    assert.equal((await (await call(`${ws}/emails`, owner)).json()).some(row => row.studentId === studentA), false);

    // The guardian leaves through the signed link; a GET only shows the button.
    const encode = value => Buffer.from(value).toString('base64url');
    const sign = email => createHmac('sha256', 'course-management-development-unsubscribe-key-only')
      .update(`${workspaceId.replaceAll('-', '')}:${email}`).digest('base64url');
    const query = `w=${workspaceId.replaceAll('-', '')}&e=${encode(guardianEmail)}&t=${sign(guardianEmail)}`;
    const unsubscribeBase = `${base}/api/course-management/public/email/unsubscribe`;
    const confirmPage = await fetch(`${unsubscribeBase}?${query}`);
    assert.equal(confirmPage.status, 200);
    assert.match(await confirmPage.text(), /<form method="post">/);
    assert.match(await (await fetch(`${unsubscribeBase}?w=${workspaceId.replaceAll('-', '')}&e=${encode(guardianEmail)}&t=bad`)).text(),
      /Bağlantı geçersiz/);
    const left = await fetch(`${unsubscribeBase}?${query}`, { method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'List-Unsubscribe=One-Click' });
    assert.equal(left.status, 200);
    assert.match(await left.text(), /E-postalardan çıktınız/);

    // A payment is told only to addresses that still want e-mails: here nobody.
    const payment = await call(`${ws}/finance/payments`, owner, 'POST', { id: randomUUID(), studentId: studentB, method: 1,
      amount: 100, receivedAt: new Date().toISOString(), note: null, allocations: [] });
    assert.equal(payment.status, 201, await payment.clone().text());
    const paymentEmail = await waitFor(row => row.kind === 'PaymentReceived', 'payment');
    assert.equal(paymentEmail.status, 'Skipped');
    assert.equal(paymentEmail.skipReason, 'Alıcılar e-postalardan çıktı');

    const muted = await call(`${ws}/students/${studentB}/email-mute`, owner, 'PUT', { muted: true });
    assert.deepEqual(await muted.json(), { studentId: studentB, emailsMuted: true });
    assert.equal((await call(`${ws}/students/${studentB}/email-mute`, teacher, 'PUT', { muted: false })).status, 404);
    const testEmail = await call(`${ws}/email-settings/test`, owner, 'POST', { kind: 1, email: 'sahip@example.test' });
    assert.equal(testEmail.status, 202);
  } finally {
    child.kill();
  }
});

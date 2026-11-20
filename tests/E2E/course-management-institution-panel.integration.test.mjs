import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHmac, randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { createServer as createHttpServer } from 'node:http';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// First release (19.11.2026): an institution panel only. Student portal and homework files are off by default;
// the teacher marks homework in class and sees no finance.
const root = fileURLToPath(new URL('../../', import.meta.url));
const secret = 'course-management-institution-panel-test-secret-0123456789012345';
const connectionString = process.env.COURSE_MANAGEMENT_TEST_CONNECTION_STRING;

function token(product, sub, role, extra = {}) {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const payload = encode({ iss: 'course-tests', aud: 'course-tests', sub, platform_product: product,
    exp: Math.floor(Date.now() / 1000) + 300, 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role, ...extra });
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
  // Stand-ins for Identity (the owner's address for support notices) and the notification service (delivery results).
  const internalKey = 'course-management-test-internal-key-0123456789';
  const deliveryCalls = [];
  // The administrators' MFA policy for Kurs Yönetimi, as Identity would answer it; the test switches it.
  let mfaMode = 'mutations';
  const stub = createHttpServer(async (request, response) => {
    let body = '';
    for await (const chunk of request) body += chunk;
    const json = body ? JSON.parse(body) : {};
    const reply = value => { response.writeHead(200, { 'Content-Type': 'application/json' }); response.end(JSON.stringify(value)); };
    if (request.headers['x-internal-service-key'] !== internalKey) { response.writeHead(401); response.end(); return; }
    if (request.url === '/api/internal/mfa-policy/course-management') return reply({ mode: mfaMode });
    if (request.url === '/api/internal/notifications/email/status') {
      deliveryCalls.push(json);
      // Every institution e-mail bounced in this test.
      return reply(json.messageIds.map(messageId => ({ messageId, status: 'Failed' })));
    }
    response.writeHead(404); response.end();
  });
  await new Promise(done => stub.listen(0, '127.0.0.1', done));
  const stubBase = `http://127.0.0.1:${stub.address().port}`;
  // No feature switches are set: the defaults are what production gets.
  const child = spawn('dotnet', [resolve(root, 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll')], {
    cwd: root, stdio: process.env.COURSE_MANAGEMENT_TEST_API_LOG === '1' ? 'inherit' : 'ignore', env: { ...process.env,
      ASPNETCORE_URLS: base, ASPNETCORE_ENVIRONMENT: 'Development',
      JWT_SECRET: secret, JWT_ISSUER: 'course-tests', JWT_AUDIENCE: 'course-tests',
      CourseManagement__OutboxQueryDelaySeconds: '3600', CourseManagement__ReminderPollingSeconds: '3600',
      // E-mails go out as soon as they are found, checked every second.
      CourseManagement__Email__DelayMinutes: '0', CourseManagement__Email__PollingSeconds: '1',
      CourseManagement__Email__DeliveryCheckSeconds: '1',
      Services__IdentityService: stubBase, Services__NotificationService: stubBase, INTERNAL_SERVICE_API_KEY: internalKey,
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

    // maxkampus.com (19.11.2026): a teacher is invited by address; the code travels only in the e-mail link.
    const emailInviteId = randomUUID();
    const emailInvite = await call(`${ws}/teacher-email-invitations`, owner, 'POST', { id: emailInviteId, email: ' TEST-Davet@Example.com ' });
    assert.equal(emailInvite.status, 200, await emailInvite.clone().text());
    assert.deepEqual((({ email, status }) => ({ email, status }))(await emailInvite.json()), { email: 'test-davet@example.com', status: 'Pending' });
    // Sending again renews the same invitation instead of opening a second one.
    assert.equal((await call(`${ws}/teacher-email-invitations`, owner, 'POST', { id: randomUUID(), email: 'test-davet@example.com' })).status, 200);
    assert.equal((await (await call(`${ws}/teacher-email-invitations`, owner)).json()).length, 1);
    assert.equal((await call(`${ws}/teacher-email-invitations`, teacher)).status, 404, 'only the owner sees invitations');
    const unknownCode = 'a'.repeat(64);
    assert.equal((await fetch(`${base}/api/course-management/public/teacher-invitations/${unknownCode}`)).status, 404);
    assert.equal((await fetch(`${base}/api/course-management/public/teacher-invitations/kisa`)).status, 404);
    assert.equal((await call(`/me/teacher-invitations/${unknownCode}/accept`, teacher, 'POST', { displayName: 'TEST' })).status, 404);
    assert.equal((await call(`${ws}/teacher-email-invitations/${emailInviteId}/cancel`, owner, 'POST')).status, 204);
    assert.equal((await (await call(`${ws}/teacher-email-invitations`, owner)).json())[0].status, 'Cancelled');

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

    // Setup guide (20.11.2026): progress read from the data, the institution's kind, invited teachers given work before
    // they accept, group edits and one-to-one teachers.
    const setup = await (await call(`${ws}/setup`, owner)).json();
    assert.deepEqual(setup.steps.map(step => step.id), ['profile', 'teachers', 'rooms', 'courses', 'students', 'fees', 'schedule', 'emails']);
    assert.equal(setup.steps.find(step => step.id === 'courses').status, 'done');
    assert.equal(setup.steps.find(step => step.id === 'students').status, 'done');
    assert.equal(setup.steps.find(step => step.id === 'profile').status, 'open');
    assert.equal((await call(`${ws}/setup`, teacher)).status, 404, 'the guide is the owner\'s');
    const profile = await call(`${ws}/profile`, owner, 'PUT', { name: 'TEST Kurs', institutionCategory: 3 });
    assert.equal(profile.status, 200, await profile.clone().text());
    assert.equal((await profile.json()).institutionCategory, 'StudyCenter');
    assert.equal((await call(`${ws}/profile`, owner, 'PUT', { name: ' ', institutionCategory: null })).status, 400);
    assert.equal((await call(`${ws}/setup`, owner, 'PUT', { skipped: ['rooms', 'bogus'], closed: false })).status, 204);
    const afterSkip = await (await call(`${ws}/setup`, owner)).json();
    assert.equal(afterSkip.steps.find(step => step.id === 'rooms').status, 'skipped');
    assert.equal(afterSkip.steps.find(step => step.id === 'profile').status, 'done');
    assert.equal(afterSkip.closed, false);

    const pendingId = randomUUID();
    const pending = await call(`${ws}/teacher-email-invitations`, owner, 'POST', { id: pendingId, email: 'test-bekleyen@example.com', displayName: 'TEST Bekleyen' });
    assert.equal(pending.status, 200, await pending.clone().text());
    assert.equal((await pending.json()).displayName, 'TEST Bekleyen');
    const pendingGroupId = randomUUID();
    const pendingGroup = await call(`${ws}/groups`, owner, 'POST', { id: pendingGroupId, courseId, teacherUserId: pendingId, name: 'TEST 8-B' });
    assert.equal(pendingGroup.status, 201, `an invited teacher can be given a group: ${await pendingGroup.clone().text()}`);
    assert.equal((await call(`${ws}/groups`, owner, 'POST', { id: randomUUID(), courseId, teacherUserId: randomUUID(), name: 'TEST yok' })).status >= 400, true,
      'a made-up teacher is refused');
    const renamed = await call(`${ws}/groups/${pendingGroupId}`, owner, 'PUT', { name: 'TEST 8-C', teacherUserId: teacherId, moveFutureLessons: true, force: false });
    assert.equal(renamed.status, 200, await renamed.clone().text());
    assert.deepEqual((({ name, teacherUserId }) => ({ name, teacherUserId }))(await renamed.json()), { name: 'TEST 8-C', teacherUserId: teacherId });
    assert.equal((await call(`${ws}/groups/${pendingGroupId}`, teacher, 'PUT', { name: 'X', teacherUserId: null, moveFutureLessons: false, force: false })).status, 404);

    const soloId = randomUUID();
    assert.equal((await call(`${ws}/students`, owner, 'POST', { id: soloId, displayName: 'TEST Birebir', email: null, phoneNumber: null })).status, 201);
    const teacherBefore = await (await call(`${ws}/students/search?take=50`, teacher)).json();
    assert.equal(teacherBefore.items.some(item => item.id === soloId), false);
    const solo = await call(`${ws}/students/${soloId}/one-to-one-teacher`, owner, 'PUT', { teacherUserId: teacherId });
    assert.equal(solo.status, 200, await solo.clone().text());
    const teacherAfter = await (await call(`${ws}/students/search?take=50`, teacher)).json();
    assert.equal(teacherAfter.items.find(item => item.id === soloId)?.oneToOneTeacherUserId, teacherId, 'the one-to-one teacher sees the student');
    assert.equal((await call(`${ws}/students/${soloId}/one-to-one-teacher`, owner, 'PUT', { teacherUserId: pendingId })).status, 200);
    assert.equal((await call(`${ws}/students/${soloId}/one-to-one-teacher`, owner, 'PUT', { teacherUserId: randomUUID() })).status, 400);
    // The rest of this test counts two active students: the one-to-one test student is archived again.
    assert.equal((await call(`${ws}/students/${soloId}`, owner, 'PUT', { displayName: 'TEST Birebir', email: null, phoneNumber: null, isActive: false })).status, 200);

    const extraGroupId = randomUUID();
    assert.equal((await call(`${ws}/groups`, owner, 'POST', { id: extraGroupId, courseId, teacherUserId: teacherId, name: 'TEST 8-D' })).status, 201);
    assert.equal((await call(`${ws}/groups/${extraGroupId}/students/${studentA}`, owner, 'POST')).status, 201);
    assert.equal((await call(`${ws}/groups/${extraGroupId}/students/${studentA}`, owner, 'DELETE')).status, 204);
    assert.equal((await call(`${ws}/groups/${extraGroupId}/students/${studentA}`, owner, 'DELETE')).status, 204, 'leaving twice is harmless');
    assert.equal((await call(`${ws}/groups/${extraGroupId}/students/${studentA}`, teacher, 'DELETE')).status, 404);

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

    // A series is previewed with its clashes and saved without them (19.11.2026).
    const pad = value => String(value).padStart(2, '0');
    const turkeyDay = offset => { const day = new Date(Date.now() + 3 * 3600000 + offset * 86400000);
      return `${day.getUTCFullYear()}-${pad(day.getUTCMonth() + 1)}-${pad(day.getUTCDate())}`; };
    const seriesBody = extra => ({ id: randomUUID(), courseId, groupId, studentId: null, teacherUserId: teacherId, timeZoneId: null,
      startsOn: turkeyDay(1), endsOn: turkeyDay(7), startTime: '15:00', durationMinutes: 60, daysOfWeekMask: 127, roomId: null, ...extra });
    const seriesPreview = await (await call(`${ws}/lesson-series`, owner, 'POST', seriesBody({ preview: true }))).json();
    assert.equal(seriesPreview.sessionCount, 7);
    assert.equal(seriesPreview.conflictCount, 0);
    const clash = await call(`${ws}/lessons`, owner, 'POST', { id: randomUUID(), courseId, groupId, studentId: null, teacherUserId: teacherId,
      startsAt: `${turkeyDay(2)}T15:30:00+03:00`, endsAt: `${turkeyDay(2)}T16:00:00+03:00` });
    assert.equal(clash.status, 201, await clash.clone().text());
    const clashing = await (await call(`${ws}/lesson-series`, owner, 'POST', seriesBody({ preview: true }))).json();
    assert.equal(clashing.conflictCount, 1);
    assert.deepEqual(clashing.occurrences.find(item => item.conflicts.length).conflicts, ['Lesson.TeacherConflict', 'Lesson.StudentConflict']);
    assert.equal((await call(`${ws}/lesson-series`, owner, 'POST', seriesBody({}))).status, 409, 'a clash stops a plain save');
    const savedSeries = await (await call(`${ws}/lesson-series`, owner, 'POST', seriesBody({ skipConflicts: true }))).json();
    assert.equal(savedSeries.sessionsCreated, 6);
    assert.equal(savedSeries.sessionsSkipped, 1);
    const monthly = await (await call(`${ws}/lesson-series`, owner, 'POST', seriesBody({ preview: true, startTime: '18:00',
      endsOn: turkeyDay(120), daysOfWeekMask: 1, monthlyWeekOfMonth: 1 }))).json();
    assert.ok(monthly.sessionCount >= 3 && monthly.sessionCount <= 5, 'the first Monday of about four months');

    // The first page: the owner sees money and e-mails, the teacher only their own lessons.
    const ownerOverview = await (await call(`${ws}/overview`, owner)).json();
    assert.equal(ownerOverview.role, 'Owner');
    assert.equal(ownerOverview.finance.monthCollections, 100);
    const teacherOverview = await (await call(`${ws}/overview`, teacher)).json();
    assert.equal(teacherOverview.role, 'Teacher');
    assert.equal(teacherOverview.finance, null);
    assert.equal((await call(`${ws}/overview`, outsider)).status, 404);
    const members = await (await call(`${ws}/members`, owner)).json();
    assert.equal(members.find(member => member.userId === teacherId).displayName, 'TEST Öğretmen');

    // Tahsilat lists: payments with filters, debts by state; owner only.
    const payments = await (await call(`${ws}/finance/payments?method=1&q=mehmet`, owner)).json();
    assert.equal(payments.total, 1);
    assert.equal(payments.sum, 100);
    assert.equal((await (await call(`${ws}/finance/payments?method=2`, owner)).json()).total, 0);
    assert.equal((await call(`${ws}/finance/payments`, teacher)).status, 404);
    const debts = await (await call(`${ws}/finance/installments?status=overdue`, owner)).json();
    assert.equal(debts.total, 0);
    assert.equal((await call(`${ws}/finance/installments?status=nope`, owner)).status, 400);

    // The student list: search, filters and pages; a teacher sees no contact details or money.
    const search = (query, bearer = owner) => call(`${ws}/students/search?${query}`, bearer).then(response => response.json());
    assert.equal((await search('q=mehmet')).total, 1);
    assert.equal((await search('q=TEST%20Veli')).items[0].displayName, 'TEST Mehmet', 'guardian names are searched too');
    assert.equal((await search(`groupId=${groupId}&take=1&sort=-name`)).items[0].displayName, 'TEST Mehmet');
    assert.equal((await search(`groupId=${groupId}&take=1&skip=1&sort=-name`)).items[0].displayName, 'TEST Ayşe');
    assert.equal((await search('status=archived')).total, 1, 'only the archived one-to-one test student');
    const teacherView = await search('', teacher);
    assert.equal(teacherView.total, 2);
    assert.equal(teacherView.items[0].email, null);
    assert.equal(teacherView.items[0].overdueAmount, null);
    assert.equal((await call(`${ws}/students/search?take=500`, owner)).status, 400);

    // Support (user decision 19.11.2026): no owner approval; the second factor follows the administrators' MFA policy,
    // and every look and change is in the institution's support record.
    const adminId = randomUUID();
    const admin = token('coaching', adminId, 'SystemAdmin', { amr: 'mfa' });
    const adminWithoutMfa = token('coaching', adminId, 'SystemAdmin');
    const adminBase = `/admin/workspaces/${workspaceId}`;
    const listed = await (await call(`/admin/workspaces?query=TEST%20Kurs`, admin)).json();
    assert.ok(listed.items.some(item => item.id === workspaceId && item.activeStudentCount === 2 && item.teacherCount === 1));
    const summary = await (await call(adminBase, admin)).json();
    assert.equal(summary.email.switchedOn, true);
    assert.equal(summary.email.optedOutAddresses, 1);
    assert.equal((await call(`/admin/workspaces/${randomUUID()}/support/students`, admin)).status, 404, 'unknown institution');
    assert.equal((await call(`${adminBase}/support/students`, owner)).status, 403, 'only administrators');

    mfaMode = 'required';
    const blocked = await call(`${adminBase}/support/students`, adminWithoutMfa);
    assert.equal(blocked.status, 403, 'every operation asks for the second factor when the policy says so');
    assert.equal((await blocked.json()).code, 'Auth.MfaRequired');
    const students = await call(`${adminBase}/support/students`, admin);
    assert.equal(students.status, 200, 'no grant is needed');
    assert.equal((await students.json()).length, 2);

    mfaMode = 'mutations';
    assert.equal((await call(`${adminBase}/support/finance`, adminWithoutMfa)).status, 200, 'looking needs no second factor in "changes only"');
    const unprotectedChange = await call(`${adminBase}/support/students/${studentA}`, adminWithoutMfa, 'PUT',
      { displayName: 'TEST Ayşe Y', email: null, phoneNumber: null });
    assert.equal(unprotectedChange.status, 403, 'a change still does');

    mfaMode = 'disabled';
    assert.equal((await call(`${adminBase}/support/settings`, adminWithoutMfa)).status, 200, 'policy off: no second factor at all');
    mfaMode = 'mutations';

    const looks = (await (await call(`${ws}/support-access`, owner)).json()).logs.filter(item => !item.isChange);
    assert.deepEqual(looks.map(item => item.section).sort(), ['Finance', 'Settings', 'Students']);
    assert.equal((await call(`${ws}/support-access`, teacher)).status, 404, 'the support record is the owner\'s');

    // Delivery is read back: the stand-in says every address bounced, and the sending record shows it.
    const bounced = await waitFor(row => row.id === homework.id && row.delivery === 'Failed', 'delivery result');
    assert.equal(bounced.failedRecipientCount, 1);
    assert.ok(deliveryCalls.every(item => item.consumerType === 'CourseManagementEmailConsumer'));
    assert.equal((await (await call(adminBase, admin)).json()).email.undeliveredLast30Days, 1);

    // Corrections: no grant needed, and they tell no one.
    const lessonRules = { ...settings(true), rules: [...settings(true).rules, { kind: 5, enabled: true, audience: 3, note: null }] };
    assert.equal((await call(settingsPath, owner, 'PUT', lessonRules)).status, 200);
    const fixedStudent = await call(`${adminBase}/support/students/${studentA}`, admin, 'PUT',
      { displayName: 'TEST Ayşe Yılmaz', email: 'ayse@example.test', phoneNumber: null });
    assert.equal(fixedStudent.status, 200, await fixedStudent.clone().text());
    const supportStudents = await (await call(`${adminBase}/support/students`, admin)).json();
    const guardianRow = supportStudents.find(item => item.id === studentB).guardians[0];
    assert.equal((await call(`${adminBase}/support/guardians/${guardianRow.id}`, admin, 'PUT',
      { displayName: 'TEST Veli Mehmet', email: guardianEmail, phoneNumber: '5550000000' })).status, 200);

    const supportLesson = randomUUID();
    const ownerLesson = randomUUID();
    for (const [id, hour] of [[supportLesson, 10], [ownerLesson, 12]])
      assert.equal((await call(`${ws}/lessons`, owner, 'POST', { id, courseId, groupId, studentId: null, teacherUserId: teacherId,
        startsAt: `${turkeyDay(10)}T${hour}:00:00+03:00`, endsAt: `${turkeyDay(10)}T${hour}:45:00+03:00` })).status, 201);
    assert.equal((await (await call(`${adminBase}/support/lessons/${supportLesson}/students/${studentA}/attendance`, admin, 'PUT', { status: 2 })).json()).code,
      'Attendance.LessonNotStarted');
    const moved = await call(`${adminBase}/support/lessons/${supportLesson}/schedule`, admin, 'PUT',
      { startsAt: `${turkeyDay(10)}T16:00:00+03:00`, endsAt: `${turkeyDay(10)}T16:45:00+03:00`, roomId: null });
    assert.equal(moved.status, 200, await moved.clone().text());
    assert.equal((await (await call(`${adminBase}/support/lessons/${ownerLesson}/schedule`, admin, 'PUT',
      { startsAt: `${turkeyDay(10)}T16:15:00+03:00`, endsAt: `${turkeyDay(10)}T17:00:00+03:00`, roomId: null })).json()).code,
      'Lesson.TeacherConflict', 'support moves are checked like the institution\'s own');
    // The support move tells no one; the owner's own move is told to families (a record appears).
    const lessonMails = async () => (await (await call(`${ws}/emails?take=200`, owner)).json()).filter(row => row.kind === 'LessonChanged');
    await new Promise(done => setTimeout(done, 3000));
    assert.equal((await lessonMails()).length, 0, 'a support correction sends no lesson-change e-mail');
    assert.equal((await call(`${ws}/lessons/${ownerLesson}/schedule`, owner, 'PUT',
      { startsAt: `${turkeyDay(10)}T13:00:00+03:00`, endsAt: `${turkeyDay(10)}T13:45:00+03:00`, roomId: null })).status, 200);
    await waitFor(row => row.kind === 'LessonChanged', 'the owner\'s lesson change');

    const switched = await call(`${adminBase}/support/settings/email/LessonChanged/off`, admin, 'POST');
    assert.equal(switched.status, 200, await switched.clone().text());
    assert.equal((await (await call(settingsPath, owner)).json()).rules.find(rule => rule.kindValue === 5).enabled, false);

    const changeLog = (await (await call(`${ws}/support-access`, owner)).json()).logs.filter(item => item.isChange);
    assert.deepEqual(changeLog.map(item => item.what).sort(), ['Ders saati düzeltildi', 'E-posta bildirimi kapatıldı',
      'Veli iletişim bilgisi düzeltildi', 'Öğrenci iletişim bilgisi düzeltildi'].sort());
    const studentChange = changeLog.find(item => item.what === 'Öğrenci iletişim bilgisi düzeltildi');
    assert.equal(studentChange.target, 'TEST Ayşe Yılmaz');
    assert.equal(studentChange.after, 'ad, e-posta');
    assert.equal(changeLog.find(item => item.what === 'Ders saati düzeltildi').target, 'TEST Matematik');
    assert.equal(changeLog.find(item => item.what === 'E-posta bildirimi kapatıldı').before, 'LessonChanged');
  } finally {
    child.kill();
    stub.close();
  }
});

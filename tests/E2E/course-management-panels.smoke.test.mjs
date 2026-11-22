import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { join, resolve, extname } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const studentId = 'ce9bec0b-b8cb-4dc0-95c2-6c2067aad855';
const workspaceId = 'f5384382-060c-4cbc-a825-4d61cd509d60';
const inviteId = 'ba5a0d8d-15d3-4212-ad98-1b10d32b110f';
function jwt(product, userId = studentId) {
  return `e30.${Buffer.from(JSON.stringify({ sub: userId, platform_product: product, exp: Math.floor(Date.now() / 1000) + 900 })).toString('base64url')}.test-signature`;
}
function authorizationProduct(request) {
  const token = request.headers().authorization?.replace(/^Bearer /, '');
  return token ? JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8')).platform_product : null;
}
function dateTimeLocal(value) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}T${String(value.getHours()).padStart(2, '0')}:${String(value.getMinutes()).padStart(2, '0')}`;
}

async function withPage(folder, action, baseHref = '/') {
  const directory = resolve(root, folder);
  const basePath = baseHref === '/' ? '' : baseHref.replace(/\/$/, '');
  const server = createServer(async (request, response) => {
    let pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    if (basePath && (pathname === basePath || pathname.startsWith(`${basePath}/`))) {
      pathname = pathname.slice(basePath.length) || '/';
    }
    const path = resolve(directory, `.${pathname === '/' ? '/index.html' : pathname}`);
    if (!path.startsWith(directory)) { response.writeHead(403).end(); return; }
    try {
      const data = await readFile(path);
      response.setHeader('Content-Type', ({ '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css' })[extname(path)] ?? 'application/octet-stream');
      response.end(data);
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({
      headless: true,
      ...(process.env.COURSE_MANAGEMENT_CHROMIUM_EXECUTABLE_PATH
        ? { executablePath: process.env.COURSE_MANAGEMENT_CHROMIUM_EXECUTABLE_PATH }
        : {}),
    });
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    if (process.env.COURSE_MANAGEMENT_SMOKE_DEBUG) {
      page.on('request', request => console.log('REQUEST', request.method(), request.url()));
      page.on('response', response => console.log('RESPONSE', response.status(), response.url()));
      page.on('requestfailed', request => console.log('REQUEST FAILED', request.url(), request.failure()?.errorText));
    }
    page.setDefaultTimeout(10000);
    await action(page, `http://127.0.0.1:${server.address().port}`);
    assert.deepEqual(errors, []);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'mobile layout must fit');
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
}

async function assertPrintablePdf(page) {
  const outputDirectory = await mkdtemp(join(tmpdir(), 'course-management-print-'));
  try {
    const outputPath = join(outputDirectory, 'report.pdf');
    await page.pdf({ path: outputPath, format: 'A4', printBackground: true });
    const output = await readFile(outputPath);
    assert.equal(output.subarray(0, 5).toString('ascii'), '%PDF-');
    assert.ok(output.length > 1000, 'print view produces a non-empty PDF file');
  } finally {
    await rm(outputDirectory, { recursive: true, force: true });
  }
}

test('student signs in with an existing product, accepts invitation and joins the isolated course workspace', async () => {
  await withPage('clients/course-management/dist/course-management-student/browser', async (page, base) => {
    let accepted = false;
    let joined = false;
    let displayName = '';
    let lessonCancelled = false;
    let assignmentSubmitted = false;
    let courseNotificationRead = false;
    let courseNotificationFetchCount = 0;
    const studentSubmissionAttachments = [];
    const teacherMaterial = { id: '5feea73d-4cc0-4a7c-94c8-416f40ccae2f', fileName: 'calisma-kagidi.pdf',
      contentType: 'application/pdf', sizeBytes: 24, purpose: 'TeacherMaterial', uploadedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 180 * 86400000).toISOString() };
    const assignmentId = '2f12e0ca-36e4-47d8-ae44-ec743459488d';
    const studentLessonId = '905a9629-4a9c-4a9b-9d4c-0ce1306dfd8a';
    const studentLesson = { id: studentLessonId, workspaceName: 'Matematik', courseName: 'Matematik', subject: 'Matematik',
      groupName: '8. sınıf', teacherName: 'Öğretmen', startsAt: new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() + 49 * 60 * 60 * 1000).toISOString() };
    const studentCredits = [];
    page.on('dialog', dialog => dialog.accept());
    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      const reply = (json, status = 200) => route.fulfill({ status, json });
      if (path.endsWith('/auth/course-management/refresh-token')) return reply({ code: 'Auth.InvalidToken' }, 400);
      if (path.endsWith('/auth/captcha-config')) return reply({ enabled: false, siteKey: null });
      if (path.endsWith('/auth/course-management/login/speed-reading')) return reply({ accessToken: jwt('speed-reading') });
      if (path.endsWith('/participation/invitations')) return reply(accepted ? [] : [{ id: inviteId, workspaceName: 'Matematik', role: 'Student' }]);
      if (path.endsWith(`/${inviteId}/accept`)) {
        assert.equal(request.headers().authorization, `Bearer ${jwt('course-management')}`);
        displayName = request.postDataJSON().displayName; accepted = true;
        return reply({ workspaceId, role: 'Student' });
      }
      if (path.endsWith(`/me/lessons/${studentLessonId}/cancel-participation`) && request.method() === 'POST') {
        lessonCancelled = true;
        studentCredits.push({ id: '92e99f38-f6db-4958-ae0e-6e85788f27e2', workspaceId,
          sourceLessonId: studentLessonId, makeupLessonId: null, reason: 'StudentEarlyCancellation',
          status: 'PendingTeacherApproval', grantedAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 30 * 86400000).toISOString() });
        return reply({ lessonId: studentLessonId, eligibleForCredit: true, creditId: studentCredits[0].id, creditStatus: 'PendingTeacherApproval' });
      }
      if (path.endsWith('/auth/course-management/join')) { assert.equal(request.postDataJSON().role, 'Student'); joined = true; return route.fulfill({ status: 204 }); }
      if (path.endsWith('/auth/course-management/session')) return reply({ accessToken: jwt('course-management') });
      if (path.endsWith('/me/memberships')) { assert.equal(joined, true); return reply([{ id: workspaceId, name: 'Matematik', role: 'Student' }]); }
      if (path.endsWith('/me/courses')) return reply([]);
      if (path.endsWith('/me/lessons')) return reply(lessonCancelled ? [] : [studentLesson]);
      if (path.endsWith('/me/makeup-credits')) return reply(studentCredits);
      if (path.endsWith('/me/assignments')) return reply([{ id: assignmentId, workspaceId, workspaceName: 'Matematik',
        courseId: workspaceId, courseName: 'Kesirler', title: 'Kesirler ödevi', instructions: 'İşlemleri çözün.',
        dueAt: new Date(Date.now() + 86400000).toISOString(), status: assignmentSubmitted ? 'Submitted' : 'Pending',
        isLate: false, submissionText: assignmentSubmitted ? 'Yanıtım' : null, submittedAt: assignmentSubmitted ? new Date().toISOString() : null,
        score: null, feedback: null, reviewedAt: null, materials: [teacherMaterial], submissionAttachments: studentSubmissionAttachments }]);
      if (path.endsWith(`/me/assignments/${assignmentId}/submission`) && request.method() === 'POST') {
        assert.equal(request.postDataJSON().text, 'Yanıtım'); assignmentSubmitted = true; return reply({}, 201);
      }
      if (path.endsWith(`/me/assignments/${assignmentId}/attachments`) && request.method() === 'POST') {
        assert.match(new URL(request.url()).searchParams.get('fileName'), /cevap\.pdf$/);
        assert.match(request.postDataBuffer().toString('utf8'), /^%PDF-1\.4/);
        assignmentSubmitted = true;
        studentSubmissionAttachments.push({ id: 'f8862532-c367-433e-8be4-206629f0b55d', fileName: 'cevap.pdf',
          contentType: 'application/pdf', sizeBytes: 26, purpose: 'StudentSubmission', uploadedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 180 * 86400000).toISOString() });
        return reply(studentSubmissionAttachments[0], 201);
      }
      if (path.endsWith('/content') && path.includes('/me/assignments/'))
        return route.fulfill({ status: 200, contentType: 'application/pdf', body: Buffer.from('%PDF-1.4\nfile') });
      if (path.endsWith('/me/finance')) return reply([{ workspaceId, workspaceName: 'Matematik', balance: 250.5,
        packageBalances: [{ agreementId: 'pkg', grantedSessions: 5, remainingSessions: 3, expiresAt: null }] }]);
      if (path === '/api/course-management/notifications') {
        courseNotificationFetchCount++;
        return reply([{ id: 'course-notice-1', title: 'Yeni ders planlandı',
          message: 'Dersiniz yarın planlandı.', type: 'CourseManagement.LessonScheduled', createdAt: new Date().toISOString(), isRead: courseNotificationRead }]);
      }
      if (path.endsWith('/notifications/course-notice-1/mark-as-read')) { courseNotificationRead = true; return route.fulfill({ status: 200 }); }
      if (path.endsWith('/notifications/mark-all-as-read')) { courseNotificationRead = true; return route.fulfill({ status: 200 }); }
      if (path.endsWith('/me/attendance')) return reply([{ id: 'ec5f560a-8133-4547-a7cd-c7925d54be54', lessonId: studentLessonId,
        workspaceName: 'Matematik', courseName: 'Matematik', subject: 'Matematik', startsAt: new Date(Date.now() - 86400000).toISOString(),
        endsAt: new Date(Date.now() - 23 * 60 * 60 * 1000).toISOString(), status: 'Absent', markedAt: new Date().toISOString() }]);
      if (path.endsWith('/me/profile')) return reply({ userId: studentId, displayName });
      if (path.endsWith('/auth/course-management/revoke-token')) return route.fulfill({ status: 204 });
      throw new Error(`Unexpected student request: ${path}`);
    });
    await page.goto(base);
    await page.getByLabel('Hesabınızın kayıtlı olduğu ürün').selectOption('speed-reading');
    await page.getByLabel('E-posta', { exact: true }).fill('student@example.test');
    await page.getByLabel('Şifre', { exact: true }).fill('test-password');
    await page.getByRole('button', { name: 'Giriş yap', exact: true }).click();
    await page.getByLabel('Görünen adınız').fill('Öğrenci adı');
    await page.getByRole('button', { name: 'Daveti kabul et', exact: true }).click();
    await page.getByRole('heading', { name: 'Kayıtlı çalışma alanlarım' }).waitFor();
    await page.getByText('Davet kabul edildi.', { exact: true }).waitFor();
    await page.getByRole('heading', { name: 'Kurs bildirimlerim' }).waitFor();
    await page.getByText('Dersiniz yarın planlandı.', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Okundu işaretle', exact: true }).click();
    assert.equal(courseNotificationRead, true);
    const notificationRegion = page.getByRole('region', { name: 'Kurs bildirimlerim' });
    await notificationRegion.getByRole('button', { name: 'Yenile', exact: true }).click();
    assert.equal(courseNotificationFetchCount, 2);
    assert.equal(await page.getByText('Net bakiye').count(), 0, 'student workspace does not expose the staff finance panel');
    await page.getByRole('heading', { name: 'Finans durumum' }).waitFor();
    const studentBalanceSummary = page.getByText(/^Bakiye:/);
    await studentBalanceSummary.waitFor();
    assert.match(await studentBalanceSummary.textContent(), /250,50/, 'student sees their own balance');
    await page.getByRole('heading', { name: 'Ödevlerim' }).waitFor();
    await page.getByText('Ödev dosyaları son teslim tarihinden 180 gün sonra silinir.', { exact: true }).waitFor();
    await page.getByRole('heading', { name: 'Ders geçmişim' }).waitFor();
    await page.getByRole('heading', { name: 'Matematik: Katılmadı', exact: true }).waitFor();
    await page.getByText('Kalan ders hakkı: 3 / 5', { exact: false }).waitFor();
    const teacherDownload = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Dosyayı indir', exact: true }).click();
    assert.equal((await teacherDownload).suggestedFilename(), teacherMaterial.fileName);
    await page.getByLabel('Yanıtınız').fill('Yanıtım');
    await page.getByRole('button', { name: 'Ödevi teslim et' }).click();
    await page.getByText('Ödev tesliminiz kaydedildi.', { exact: true }).waitFor();
    assert.equal(assignmentSubmitted, true);
    await page.locator(`#assignment-files-${assignmentId}`).setInputFiles({ name: 'cevap.pdf', mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4\nstudent answer') });
    await page.getByRole('button', { name: 'Dosyaları ödev olarak teslim et' }).click();
    await page.getByText('Ödev dosyaları teslim edildi.', { exact: true }).waitFor();
    await page.getByText('Teslim ettiğiniz dosya: cevap.pdf', { exact: false }).waitFor();
    await page.getByText('Ders başlangıcına en az 24 saat varken iptal ederseniz telafi kredisi öğretmen onayına gönderilir; 24 saatten az süre kaldıysa kredi oluşmaz.', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Derse katılımımı iptal et' }).click();
    // 20.11.2026: the student answers in the page, not in the browser's confirm box.
    await page.getByText('Bu derse katılımınız iptal edilsin mi?', { exact: false }).waitFor();
    await page.getByRole('button', { name: 'Evet, iptal et' }).click();
    await page.getByText('Katılımınız iptal edildi. Telafi kredisi öğretmen onayı bekliyor.', { exact: true }).waitFor();
    await page.getByRole('heading', { name: 'Telafi haklarım' }).waitFor();
    await page.getByText('Öğretmen onayı bekliyor', { exact: true }).waitFor();
    assert.equal(displayName, 'Öğrenci adı');
    assert.equal(await page.evaluate(() => localStorage.length + sessionStorage.length), 0);
    await page.getByRole('button', { name: 'Çıkış yap' }).click();
    await page.getByRole('heading', { name: 'Mevcut hesabınızla giriş yapın' }).waitFor();
  });
});

test('student can read a suspended workspace while workspace writes stay disabled', async () => {
  await withPage('clients/course-management/dist/course-management-student/browser', async (page, base) => {
    const assignmentId = '2f12e0ca-36e4-47d8-ae44-ec743459488d';
    let workspaceWrites = 0;
    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      const reply = (json, status = 200) => route.fulfill({ status, json });
      if (path.endsWith('/auth/captcha-config')) return reply({ enabled: false, siteKey: null });
      if (path.endsWith('/auth/course-management/refresh-token')) return reply({ code: 'Auth.InvalidToken' }, 400);
      if (path.endsWith('/auth/course-management/login/course-management')) return reply({ accessToken: jwt('course-management') });
      if (path.endsWith('/participation/invitations')) return reply([{ id: inviteId, workspaceId, workspaceName: 'Matematik', role: 'Student', isReadOnly: true }]);
      if (path.endsWith('/me/memberships')) return reply([{ id: workspaceId, name: 'Matematik', role: 'Student', isReadOnly: true }]);
      if (path.endsWith('/me/profile')) return reply({ userId: studentId, displayName: 'Öğrenci adı' });
      if (path.endsWith('/me/courses') || path.endsWith('/me/makeup-credits') || path.endsWith('/me/finance')
        || path.endsWith('/me/attendance') || path === '/api/course-management/notifications') return reply([]);
      if (path.endsWith('/me/lessons')) return reply([{ id: '905a9629-4a9c-4a9b-9d4c-0ce1306dfd8a', workspaceId,
        workspaceName: 'Matematik', courseName: 'Kesirler', subject: 'Matematik', groupName: '5. sınıf', teacherName: 'Öğretmen',
        startsAt: new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString(), endsAt: new Date(Date.now() + 49 * 60 * 60 * 1000).toISOString() }]);
      if (path.endsWith('/me/assignments')) return reply([{ id: assignmentId, workspaceId, workspaceName: 'Matematik',
        courseId: workspaceId, courseName: 'Kesirler', title: 'Kesirler ödevi', instructions: null,
        dueAt: new Date(Date.now() + 86400000).toISOString(), status: 'Pending', isLate: false, submissionText: '',
        submittedAt: null, score: null, feedback: null, reviewedAt: null, materials: [{ id: 'teacher-file', fileName: 'ders.pdf',
          contentType: 'application/pdf', sizeBytes: 20, purpose: 'TeacherMaterial', uploadedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 180 * 86400000).toISOString() }], submissionAttachments: [] }]);
      if (path.startsWith('/api/course-management/v1/') && (request.method() === 'POST' || request.method() === 'PUT')) {
        workspaceWrites++; return reply({ code: 'Subscription.ReadOnly' }, 409);
      }
      throw new Error(`Unexpected read-only student request: ${path}`);
    });
    await page.goto(base);
    await page.getByLabel('E-posta', { exact: true }).fill('student@example.test');
    await page.getByLabel('Şifre', { exact: true }).fill('test-password');
    await page.getByRole('button', { name: 'Giriş yap', exact: true }).click();
    await page.getByRole('heading', { name: 'Kayıtlı çalışma alanlarım' }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Daveti kabul et', exact: true }).isDisabled(), true);
    assert.equal(await page.getByText('Bu çalışma alanı salt okunur; ders katılımı değiştirilemez.', { exact: true }).count(), 1);
    assert.equal(await page.getByLabel('Yanıtınız').isDisabled(), true);
    assert.equal(await page.getByRole('button', { name: 'Ödevi teslim et' }).isDisabled(), true);
    assert.equal(await page.locator(`#assignment-files-${assignmentId}`).count(), 0,
      'read-only workspaces do not render assignment file submission');
    assert.equal(await page.getByRole('button', { name: 'Dosyaları ödev olarak teslim et' }).count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Dosyayı indir', exact: true }).isDisabled(), false);
    assert.equal(await page.getByRole('button', { name: 'Profili kaydet', exact: true }).isDisabled(), false);
    assert.equal(workspaceWrites, 0);
  });
});

test('course teacher creates a profile, independent workspace and targeted invitation in the staff panel', async () => {
  await withPage('clients/admin-panel/dist/staff-portal/browser', async (page, base) => {
    let workspace = null;
    let invitation = null;
    let displayName = '';
    let importedStudents = 0;
    let legacyImportedStudents = [];
    let legacyImportFields = null;
    let legacyImportRows = null;
    let studentPageRequests = 0;
    let lessonPageRequests = 0;
    let assignmentPageRequests = 0;
    let financeReportRequests = 0;
    let auditRequests = 0;
    const lessonId = 'cb599577-9f0a-4b73-b796-5e629f42b978';
    const originalLessonStart = new Date(Date.now() + 5 * 24 * 60 * 60 * 1000);
    originalLessonStart.setMinutes(0, 0, 0);
    const attendanceLessonId = 'c0a32442-0353-4c11-83a2-4d2687032662';
    const attendanceLessonStart = new Date(Date.now() - 2 * 60 * 60 * 1000);
    let attendanceStatus = null;
    let attendanceLessonStatus = 'Scheduled';
    const attendanceLesson = { id: attendanceLessonId, seriesId: null, timeZoneId: null, courseId: workspaceId,
      courseName: 'Matematik', subject: 'Matematik', workspaceName: 'Matematik özel ders', groupId: null,
      groupName: null, roomId: null, roomName: null, branchName: null, teacherUserId: studentId,
      startsAt: attendanceLessonStart.toISOString(), endsAt: new Date(attendanceLessonStart.getTime() + 60 * 60 * 1000).toISOString(),
      status: attendanceLessonStatus, students: [{ studentId: inviteId, displayName: 'Ada Öğrenci', attendanceStatus }] };
    let lesson = { id: lessonId, seriesId: null, timeZoneId: null, courseId: workspaceId, courseName: 'Matematik',
      subject: 'Matematik', workspaceName: 'Matematik özel ders', groupId: null, groupName: null, roomId: null,
      roomName: null, branchName: null, teacherUserId: studentId, startsAt: originalLessonStart.toISOString(),
      endsAt: new Date(originalLessonStart.getTime() + 60 * 60 * 1000).toISOString(), status: 'Scheduled',
      students: [{ studentId: inviteId, displayName: 'Ada Öğrenci', attendanceStatus: null }] };
    const creditId = 'cef6f86a-860c-4600-bccf-9026e097915b';
    const financeStudentId = 'ce00a0ae-ccae-4cd5-aec9-113fd884ad91';
    const financeCourseId = '72990111-5842-4a3c-a78d-a1c012032c95';
    const groupId = '6306df76-6dcb-4874-b03c-9d09ff645733';
    let createdAssignmentId = null;
    const financeAgreements = [];
    const financeInstallments = [];
    const financePayments = [];
    const courseAssignments = [];
    const assignmentMaterials = [];
    let assignmentReview = null;
    const financeStudent = { id: financeStudentId, userId: null, displayName: 'Finans öğrencisi',
      email: 'private-report@example.test', phoneNumber: '5551234567', isActive: true };
    function financeAccount() {
      const totalCharges = financeInstallments.reduce((sum, item) => sum + item.amount, 0);
      const totalPayments = financePayments.reduce((sum, item) => sum + item.amount, 0);
      return { studentId: financeStudentId, balance: totalCharges - totalPayments, totalCharges, totalPayments,
        unallocatedPaymentAmount: totalPayments - financePayments.reduce((sum, item) => sum + item.allocatedAmount, 0),
        agreements: financeAgreements, installments: financeInstallments, payments: financePayments, packageBalances: [], packageMovements: [] };
    }
    function financeSummary() {
      const account = financeAccount();
      const today = new Date().toISOString().slice(0, 10);
      const firstOfMonth = `${today.slice(0, 7)}-01`;
      return { from: firstOfMonth, to: today, totalCharges: account.totalCharges, totalPayments: account.totalPayments,
        balance: account.balance, overdueAmount: 0, dueInNext30Days: 1200,
        collectionsByMethod: [{ method: 'Cash', amount: financePayments.filter(item => item.method === 'Cash').reduce((sum, item) => sum + item.amount, 0) }] };
    }
    let makeupCredit = { id: creditId, studentId: inviteId, studentName: 'Ada Öğrenci', sourceLessonId: attendanceLessonId,
      sourceLessonStartsAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), sourceCourseId: workspaceId,
      sourceTeacherUserId: studentId, durationMinutes: 60, makeupLessonId: null, reason: 'StudentEarlyCancellation',
      status: 'PendingTeacherApproval', grantedAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 30 * 86400000).toISOString() };
    let makeupLessonRequest = null;
    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      const reply = (json, status = 200) => route.fulfill({ status, json });
      if (path.endsWith('/auth/refresh-token')) return reply({ code: 'Auth.InvalidToken' }, 400);
      if (path.endsWith('/auth/course-management/refresh-token')) return reply({ accessToken: jwt('course-management'),
        expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path.endsWith('/auth/captcha-config')) return reply({ enabled: false, siteKey: null });
      if (path.endsWith('/auth/course-management/login')) return reply({ accessToken: jwt('course-management'), expiresInMinutes: 15 });
      if (path.endsWith('/auth/staff-session/products')) return reply([
        { product: 'course-management', roles: ['CourseTeacher'] },
        { product: 'coaching', roles: ['InstitutionAdmin'] },
        { product: 'speed-reading', roles: ['InstitutionOwner'] }
      ]);
      if (path.endsWith('/auth/staff-session/switch/coaching')) return reply({ accessToken: jwt('coaching'), expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path.endsWith('/auth/staff-session/switch/speed-reading')) return reply({ accessToken: jwt('speed-reading'), expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path === '/api/institution/students') {
        assert.equal(authorizationProduct(request), 'coaching', 'source roster uses a temporary coaching token');
        const query = new URL(request.url()).searchParams;
        assert.equal(query.get('page'), '1'); assert.equal(query.get('pageSize'), '100');
        return reply({ items: [{ userId: '84d38c5f-69a5-46b3-9ca0-347881a20a16', firstName: 'Aynı', lastName: 'E-posta',
          email: financeStudent.email, isActive: false }], totalCount: 1, pageNumber: 1, pageSize: 100 });
      }
      if (path === '/api/institution/speed-reading/me') {
        assert.equal(authorizationProduct(request), 'speed-reading');
        return reply({ institutionId: 'dd75860a-64bd-4090-9614-1c03ad6e534e' });
      }
      if (path === '/api/speed-reading/institutions/dd75860a-64bd-4090-9614-1c03ad6e534e/members') {
        assert.equal(authorizationProduct(request), 'speed-reading');
        const query = new URL(request.url()).searchParams;
        assert.equal(query.get('role'), 'Student'); assert.equal(query.get('pageNumber'), '1'); assert.equal(query.get('pageSize'), '100');
        return reply({ items: [{ userId: '957bb10f-899a-4d83-95d4-f09ae0ef51ce', firstName: 'Hızlı', lastName: 'Okuma',
          email: 'speed-reading@example.test', role: 'Student', isActive: true }], totalCount: 1, pageNumber: 1, pageSize: 100 });
      }
      if (path === '/api/course-management/notifications') return reply([{ id: 'staff-course-notice', title: 'Ödev teslim edildi',
        message: 'Bir öğrenci ödevini gönderdi.', type: 'CourseManagement.AssignmentSubmitted', createdAt: new Date().toISOString(), isRead: false }]);
      if (path.endsWith('/overview')) return reply({ today: new Date().toISOString().slice(0, 10),
        role: workspace?.role === 'Owner' ? 'Owner' : 'Teacher', todayLessons: [], lessonsWaitingForAttendance: [],
        finance: null, emails: null, supportAccess: null });
      if (path.endsWith('/me/profile')) {
        if (request.method() === 'PUT') displayName = request.postDataJSON().displayName;
        return displayName ? reply({ userId: studentId, displayName }) : route.fulfill({ status: 404 });
      }
      if (path.endsWith('/me/memberships')) return reply(workspace ? [workspace] : []);
      if (path.endsWith('/me/subscriptions')) return reply(workspace ? [{
        workspaceId: workspace.id, workspaceName: workspace.name, workspaceType: workspace.type,
        activeStudentCount: 0, subscription: { planName: 'Öğretmen Plus', activeStudentLimit: 25,
          startsAt: '2026-10-01T00:00:00Z', endsAt: '2027-10-01T00:00:00Z', accessStatus: 'Active' }
      }] : []);
      if (path.endsWith('/participation/invitations')) return reply([]);
      if (path.endsWith('/workspaces')) {
        const body = request.postDataJSON(); assert.equal(body.type, 2);
        workspace = { id: body.id, name: body.name, type: 'IndependentTeacher', role: 'Owner' };
        return reply(workspace, 201);
      }
      if (path.endsWith('/audit')) {
        auditRequests++;
        return reply([{ actorUserId: 'actor-private-marker', targetId: 'target-private-marker',
          action: 'Workspace.Created', occurredAt: '2026-10-13T09:00:00Z' },
        { actorUserId: 'actor-private-marker', targetId: 'target-private-marker',
          action: 'Student.Created', occurredAt: '2026-10-13T09:10:00Z' }]);
      }
      if (path.endsWith('/members')) return reply([{ userId: studentId, role: 'Owner', isActive: true }]);
      if (path.endsWith(`/lessons/${lessonId}/schedule`) && request.method() === 'PUT') {
        const body = request.postDataJSON();
        lesson = { ...lesson, ...body };
        return reply({ id: lesson.id, startsAt: lesson.startsAt, endsAt: lesson.endsAt, roomId: lesson.roomId });
      }
      if (path.endsWith(`/lessons/${attendanceLessonId}/students/${inviteId}/attendance`) && request.method() === 'PUT') {
        attendanceStatus = ({ 1: 'Present', 2: 'Absent', 3: 'Excused', 4: 'Late' })[request.postDataJSON().status];
        attendanceLesson.students[0].attendanceStatus = attendanceStatus;
        return reply({ lessonId: attendanceLessonId, studentId: inviteId, status: attendanceStatus, markedAt: new Date().toISOString() });
      }
      if (path.endsWith(`/lessons/${attendanceLessonId}/complete`) && request.method() === 'POST') {
        attendanceLessonStatus = 'Completed'; attendanceLesson.status = attendanceLessonStatus;
        return route.fulfill({ status: 204 });
      }
      if (path.endsWith(`/makeup-credits/${creditId}/approve`) && request.method() === 'POST') {
        makeupCredit = { ...makeupCredit, status: 'Available' };
        return reply({ id: creditId, status: makeupCredit.status, expiresAt: makeupCredit.expiresAt });
      }
      if (path.endsWith('/makeup-credits') && request.method() === 'GET') return reply([makeupCredit]);
      if (path.endsWith('/finance/summary') && request.method() === 'GET') return reply(financeSummary());
      if (path.endsWith('/finance/report') && request.method() === 'GET') {
        financeReportRequests++;
        return reply({ generatedOn: '2026-10-13', upcomingThrough: '2026-11-12',
          overdueInstallments: [{ installmentId: 'overdue-1', feeAgreementId: 'agreement-1', studentId: financeStudentId,
            studentName: financeStudent.displayName, courseId: financeCourseId, courseName: 'Matematik', label: 'Eylül vadesi',
            amount: 1200, remainingAmount: 400, dueOn: '2026-09-30' }],
          upcomingInstallments: [{ installmentId: 'upcoming-1', feeAgreementId: 'agreement-2', studentId: financeStudentId,
            studentName: financeStudent.displayName, courseId: financeCourseId, courseName: 'Matematik', label: 'Kasım vadesi',
            amount: 1200, remainingAmount: 900, dueOn: '2026-11-05' }],
          packageBalances: [{ agreementId: 'package-1', studentId: financeStudentId, studentName: financeStudent.displayName,
            courseId: financeCourseId, courseName: 'Matematik', amount: 1500, grantedSessions: 5,
            expiresAt: '2027-01-01T00:00:00Z', remainingSessions: 4 }] });
      }
      if (path.endsWith(`/finance/students/${financeStudentId}/account`) && request.method() === 'GET') return reply(financeAccount());
      if (path.endsWith('/assignments') && request.method() === 'POST') {
        const body = request.postDataJSON(); assert.equal(body.courseId, financeCourseId); assert.equal(body.groupId, groupId);
        assert.equal(body.studentId, null); assert.equal(body.title, 'Kesirler ödevi');
        createdAssignmentId = body.id;
        const assignment = { ...body, workspaceId: workspace.id, courseName: 'Matematik', teacherUserId: studentId,
          createdAt: new Date().toISOString(), recipientCount: 1, submissionCount: 1 };
        courseAssignments.push(assignment); return reply(assignment, 201);
      }
      if (path.endsWith('/assignments') && request.method() === 'GET') {
        const query = new URL(request.url()).searchParams;
        assert.equal(query.get('skip'), '0'); assert.equal(query.get('take'), '200');
        assignmentPageRequests++;
        return reply(courseAssignments);
      }
      if (createdAssignmentId && path.endsWith(`/assignments/${createdAssignmentId}/attachments`) && request.method() === 'GET')
        return reply(assignmentMaterials);
      if (createdAssignmentId && path.endsWith(`/assignments/${createdAssignmentId}/attachments`) && request.method() === 'POST') {
        assert.match(new URL(request.url()).searchParams.get('fileName'), /ogretmen-notu\.pdf$/);
        assert.match(request.postDataBuffer().toString('utf8'), /^%PDF-1\.4/);
        const attachment = { id: 'ad88b23b-4d49-45eb-b285-8f61e4807c24', fileName: 'ogretmen-notu.pdf',
          contentType: 'application/pdf', sizeBytes: 27, purpose: 'TeacherMaterial', uploadedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 180 * 86400000).toISOString() };
        assignmentMaterials.push(attachment);
        return reply(attachment, 201);
      }
      if (path.endsWith('/content') && path.includes('/workspaces/'))
        return route.fulfill({ status: 200, contentType: 'application/pdf', body: Buffer.from('%PDF-1.4\nfile') });
      if (createdAssignmentId && path.endsWith(`/assignments/${createdAssignmentId}/submissions`) && request.method() === 'GET') return reply([{
        studentId: financeStudentId, studentName: 'Finans öğrencisi', submissionId: 'ad4a94fc-c848-4e0c-9d6c-caa03c92ce98',
        text: '3/4', status: assignmentReview ? 'Reviewed' : 'Submitted', isLate: false,
        submittedAt: new Date().toISOString(), score: assignmentReview?.score ?? null,
        feedback: assignmentReview?.feedback ?? null, reviewedAt: assignmentReview ? new Date().toISOString() : null, attachments: []
      }]);
      if (createdAssignmentId && path.endsWith(`/assignments/${createdAssignmentId}/submissions/${financeStudentId}/grade`) && request.method() === 'PUT') {
        assignmentReview = request.postDataJSON(); return reply({ studentId: financeStudentId, studentName: 'Finans öğrencisi',
          submissionId: 'ad4a94fc-c848-4e0c-9d6c-caa03c92ce98', text: '3/4', status: 'Reviewed', isLate: false,
          submittedAt: new Date().toISOString(), score: assignmentReview.score, feedback: assignmentReview.feedback,
          reviewedAt: new Date().toISOString() });
      }
      if (path.endsWith('/finance/fee-agreements') && request.method() === 'POST') {
        const body = request.postDataJSON();
        assert.equal(body.studentId, financeStudentId);
        assert.equal(body.courseId, financeCourseId);
        const agreement = { ...body, model: ({ 1: 'Term', 2: 'MonthlyFixed', 3: 'PerLesson', 4: 'LessonPackage' })[body.model],
          billingUnit: body.billingUnit === 1 ? 'Lesson' : body.billingUnit === 2 ? 'Hour' : null,
          isActive: true, createdAt: new Date().toISOString(), closedAt: null };
        financeAgreements.push(agreement);
        return reply(agreement, 201);
      }
      if (path.endsWith('/finance/fee-agreements') && request.method() === 'GET') return reply(financeAgreements);
      if (path.includes('/finance/fee-agreements/') && path.endsWith('/installments') && request.method() === 'POST') {
        const body = request.postDataJSON();
        const agreementId = path.match(/fee-agreements\/([^/]+)\/installments/)?.[1];
        const installment = { ...body, feeAgreementId: agreementId, studentId: financeStudentId, sourceLessonId: null,
          reversalOfInstallmentId: null, remainingAmount: body.amount, createdAt: new Date().toISOString() };
        financeInstallments.push(installment);
        return reply(installment, 201);
      }
      if (path.endsWith('/finance/payments') && request.method() === 'POST') {
        const body = request.postDataJSON();
        let allocatedAmount = 0;
        for (const allocation of body.allocations) {
          const installment = financeInstallments.find(item => item.id === allocation.installmentId);
          assert.ok(installment);
          installment.remainingAmount -= allocation.amount;
          allocatedAmount += allocation.amount;
        }
        const payment = { id: body.id, studentId: body.studentId, reversalOfPaymentId: null,
          method: body.method === 1 ? 'Cash' : 'BankTransfer', amount: body.amount, receivedAt: body.receivedAt,
          note: body.note, allocatedAmount, allocations: body.allocations };
        financePayments.push(payment);
        return reply(payment, 201);
      }
      if (path.includes('/finance/payments/') && path.endsWith('/reverse') && request.method() === 'POST') {
        const originalId = path.match(/finance\/payments\/([^/]+)\/reverse/)?.[1];
        const original = financePayments.find(item => item.id === originalId);
        assert.ok(original);
        for (const allocation of original.allocations) {
          const installment = financeInstallments.find(item => item.id === allocation.installmentId);
          installment.remainingAmount += allocation.amount;
        }
        const reversal = { id: request.postDataJSON().id, studentId: financeStudentId, reversalOfPaymentId: originalId,
          method: original.method, amount: -original.amount, receivedAt: new Date().toISOString(), note: 'Panelden ters kayıt',
          allocatedAmount: -original.allocatedAmount };
        financePayments.push(reversal);
        return reply(reversal, 201);
      }
      if (path.endsWith('/lessons') && request.method() === 'POST') {
        makeupLessonRequest = request.postDataJSON();
        assert.equal(makeupLessonRequest.makeupCreditId, creditId);
        assert.equal(makeupLessonRequest.studentId, inviteId);
        makeupCredit = { ...makeupCredit, status: 'Scheduled', makeupLessonId: makeupLessonRequest.id };
        return reply({ id: makeupLessonRequest.id }, 201);
      }
      // The panel's student list (21.11.2026: the statement moved to the student card).
      if (path.endsWith('/students/search')) return reply({ total: 1, skip: 0, take: 25, items: [{ ...financeStudent,
        createdAt: '2026-11-01T00:00:00Z', emailsMuted: false, oneToOneTeacherUserId: null, groups: [], guardianCount: 0,
        guardianWithoutEmail: false, overdueAmount: null }] });
      if (path.endsWith('/sent-emails') || path.endsWith('/emails')) return reply([]);
      if (request.method() === 'GET' && /\/(students|courses|groups|branches|rooms|guardians|lessons|lesson-series|teacher-time-off|teacher-availability)$/.test(path)) {
        if (path.endsWith('/students')) {
          const query = new URL(request.url()).searchParams;
          assert.equal(query.get('skip'), '0'); assert.equal(query.get('take'), '200');
          studentPageRequests++;
          return reply([financeStudent, ...legacyImportedStudents]);
        }
        if (path.endsWith('/lessons')) {
          const query = new URL(request.url()).searchParams;
          assert.equal(query.get('skip'), '0'); assert.equal(query.get('take'), '500');
          assert.ok(query.has('from') && query.has('to'));
          lessonPageRequests++;
          return reply([lesson, attendanceLesson]);
        }
        if (path.endsWith('/courses')) return reply([{ id: financeCourseId, name: 'Matematik', subject: 'Matematik' }]);
        if (path.endsWith('/groups')) return reply([{ id: groupId, courseId: financeCourseId, courseName: 'Matematik', name: '8. sınıf', teacherUserId: studentId }]);
        return reply([]);
      }
      if (path.endsWith('/invitations')) {
        if (request.method() === 'POST') {
          const body = request.postDataJSON(); assert.equal(body.targetUserId, workspaceId); assert.equal(body.role, 1);
          assert.equal(body.studentRecordId, financeStudentId, 'the owner explicitly selects the existing local student record');
          invitation = { ...body, status: 'Pending', expiresAt: new Date(Date.now() + 86400000).toISOString() };
          return reply(invitation, 201);
        }
        return reply(invitation ? [invitation] : []);
      }
      if (path.endsWith('/imports/students/preview')) {
        return reply({ previewId: 'course-import-preview', readyCount: importedStudents ? 0 : 1, importedCount: importedStudents,
          invalidCount: 0, duplicateCount: 0, rows: [{ rowNumber: 2, status: importedStudents ? 'Imported' : 'Ready',
            displayName: 'Ada Öğrenci', email: 'ada@example.test', phoneNumber: null, error: null }] });
      }
      if (path.endsWith('/imports/students') && request.method() === 'POST') {
        const body = request.postDataJSON(); assert.equal(body.previewId, 'course-import-preview');
        assert.match(body.csv, /ada@example\.test/); importedStudents++;
        return reply({ previewId: body.previewId, createdCount: 1, alreadyImportedCount: 0 });
      }
      if (path.endsWith('/imports/legacy-students/preview')) {
        assert.equal(authorizationProduct(request), 'course-management',
          'Course Management requests keep using the active product token after reading a source roster');
        const body = request.postDataJSON();
        legacyImportFields = body.selectedFields;
        legacyImportRows = body.rows;
        assert.equal(body.sourceSystem, 'coaching');
        const includeEmail = body.selectedFields.includes('email');
        if (!includeEmail) {
          assert.equal(body.rows[0].email, null, 'unselected email is not sent to Course Management');
          assert.equal(body.rows[0].isActive, true, 'unselected status is not sent to Course Management');
        }
        const conflict = includeEmail;
        return reply({ previewId: conflict ? 'legacy-conflict' : 'legacy-ready', sourceSystem: 'coaching',
          selectedFields: body.selectedFields, readyCount: conflict ? 0 : 1, importedCount: 0, invalidCount: 0,
          conflictCount: conflict ? 1 : 0, duplicateCount: 0, rows: [{ rowNumber: 1, status: conflict ? 'Conflict' : 'Ready',
            displayName: 'Aynı E-posta', email: includeEmail ? financeStudent.email : null, isActive: body.rows[0].isActive,
            error: conflict ? 'E-posta çalışma alanındaki başka bir kayıtla çakışıyor; otomatik eşleştirme yapılmadı.' : null }] });
      }
      if (path.endsWith('/imports/legacy-students') && request.method() === 'POST') {
        assert.equal(authorizationProduct(request), 'course-management');
        const body = request.postDataJSON();
        assert.equal(body.previewId, 'legacy-ready');
        assert.deepEqual(body.selectedFields, ['displayName']);
        assert.deepEqual(legacyImportRows, body.rows, 'commit must use the exact owner-selected preview payload');
        assert.equal(body.rows[0].email, null);
        assert.equal(body.rows[0].isActive, true);
        legacyImportedStudents = [{ id: 'd981c7d3-977e-42ad-a182-00bf51630143', userId: null,
          displayName: 'Aynı E-posta', email: null, phoneNumber: null, isActive: true }];
        return reply({ previewId: body.previewId, createdCount: 1, alreadyImportedCount: 0 });
      }
      // The overview asks whether any e-mail to parents is on (20.11.2026).
      if (path.endsWith('/email-settings') && request.method() === 'GET') return reply({ rules: [], installmentDaysBefore: 3,
        installmentOnDueDay: true, overdueDaysAfter: 3, sendHour: 9, replyToEmail: null, informedConsentAt: null });
      // The panel asks which switched features are on (21.11.2026); both stay off here.
      if (path === '/api/course-management/v1/features') return reply({ studentPortal: false, assignmentAttachments: false });
      throw new Error(`Unexpected staff request: ${path}`);
    });
    await page.goto(`${base}/?product=course-management`);
    await page.getByLabel('E-posta adresi').fill('teacher@example.test');
    await page.getByLabel('Şifre', { exact: true }).fill('test-password');
    await page.getByRole('button', { name: 'Personel olarak giriş yap' }).click();
    // The new panel opens with a setup card; the earlier screen stays reachable as "Önceki ekran" for its remaining tools.
    await page.getByLabel('Adınız').fill('Öğretmen adı');
    await page.getByLabel('Özel ders alanının adı').fill('Matematik özel ders');
    await page.getByRole('button', { name: 'Özel ders alanını aç', exact: true }).click();
    await page.getByRole('navigation', { name: 'maxkampüs' }).waitFor();
    // The earlier screen was removed on 21.11.2026: no menu entry, and its old address opens Genel bakış.
    assert.equal(await page.getByRole('link', { name: 'Önceki ekran' }).count(), 0);
    await page.evaluate(() => { location.hash = '#onceki-ekran'; });
    await page.waitForFunction(() => location.hash === '#genel-bakis');

    // Raporlar: the earlier screen's CSV reports, each from its own paged request.
    await page.getByRole('link', { name: 'Raporlar' }).click();
    const reportCsv = async title => {
      const card = page.locator('section.card').filter({ has: page.getByRole('heading', { name: title, exact: true }) });
      const download = page.waitForEvent('download');
      await card.getByRole('button', { name: 'CSV indir' }).click();
      return readFile(await (await download).path(), 'utf8');
    };
    const studentRequestsBeforeReport = studentPageRequests;
    const rosterCsv = await reportCsv('Öğrenci listesi');
    assert.match(rosterCsv, /Finans öğrencisi/);
    assert.doesNotMatch(rosterCsv, /private-report@example\.test|5551234567/);
    assert.equal(studentPageRequests, studentRequestsBeforeReport + 1, 'the roster report fetches its own page');
    const lessonRequestsBeforeReport = lessonPageRequests;
    const attendanceCsv = await reportCsv('Ders ve yoklama');
    assert.match(attendanceCsv, /Ada Öğrenci/);
    assert.equal(lessonPageRequests, lessonRequestsBeforeReport + 1, 'the attendance report fetches its own date range');
    await reportCsv('Ödev durumu');
    assert.ok(assignmentPageRequests > 0, 'the homework report requests paged data from the API');
    const debtsCsv = await reportCsv('Borç ve paket durumu');
    assert.match(debtsCsv, /Kullanılmamış ders paketi/);
    assert.doesNotMatch(debtsCsv, /private-report@example\.test|5551234567|ÖZEL TAHSİLAT NOTU/);
    const summaryCsv = await reportCsv('Tahsilat özeti');
    assert.match(summaryCsv, /"Toplam borç"/);
    assert.ok(financeReportRequests > 0, 'finance reports load from the owner-only finance API');

    // The student's statement moved to the student card (Öğrenciler).
    await page.getByRole('link', { name: 'Öğrenciler' }).click();
    await page.getByRole('button', { name: /Finans öğrencisi/ }).first().click();
    const statementDownload = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Ekstreyi indir (CSV)' }).click();
    const statementFile = await statementDownload;
    assert.match(statementFile.suggestedFilename(), /^ogrenci-ekstresi-[0-9a-f-]+\.csv$/i);
    assert.equal(displayName, 'Öğretmen adı');
  }, '/staff/');
});

test('institution owner imports selected legacy teachers and institution fields into the existing workspace', async () => {
  await withPage('clients/admin-panel/dist/staff-portal/browser', async (page, base) => {
    const legacyTeacherId = '93257975-b46b-45bb-9bba-fbc8bceaf1f2';
    const sourceInstitutionId = 'ed6281f9-a636-4d83-a2a2-a711637ec297';
    const invitations = [];
    let workspace = { id: workspaceId, name: 'Mevcut Kurs Kurumu', type: 'Institution',
      institutionCategory: null, role: 'Owner', isReadOnly: false };
    let invitationBody = null;
    let teacherRosterRequests = 0;
    let institutionPreviewBody = null;
    let institutionCommitBody = null;
    let workspaceCreateRequests = 0;

    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      const reply = (json, status = 200) => route.fulfill({ status, json });
      if (path.endsWith('/auth/refresh-token')) return reply({ code: 'Auth.InvalidToken' }, 400);
      if (path.endsWith('/auth/course-management/refresh-token'))
        return reply({ accessToken: jwt('course-management'), expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path.endsWith('/auth/captcha-config')) return reply({ enabled: false, siteKey: null });
      if (path.endsWith('/auth/course-management/login'))
        return reply({ accessToken: jwt('course-management'), expiresInMinutes: 15 });
      if (path.endsWith('/auth/staff-session/products')) return reply([
        { product: 'course-management', roles: ['CourseInstitutionOwner'] },
        { product: 'coaching', roles: ['InstitutionAdmin'] }
      ]);
      if (path.endsWith('/auth/staff-session/switch/coaching'))
        return reply({ accessToken: jwt('coaching'), expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path === '/api/course-management/notifications') return reply([]);
      if (path.endsWith('/overview')) return reply({ today: new Date().toISOString().slice(0, 10),
        role: workspace?.role === 'Owner' ? 'Owner' : 'Teacher', todayLessons: [], lessonsWaitingForAttendance: [],
        finance: null, emails: null, supportAccess: null });
      if (path.endsWith('/me/profile')) return reply({ userId: studentId, displayName: 'Kurum Sahibi' });
      if (path.endsWith('/me/memberships')) return reply([workspace]);
      if (path.endsWith('/me/subscriptions')) return reply([{
        workspaceId, workspaceName: workspace.name, workspaceType: workspace.type, activeStudentCount: 0,
        subscription: { planName: 'Kurum Plus', activeStudentLimit: 100,
          startsAt: '2026-10-01T00:00:00Z', endsAt: '2027-10-01T00:00:00Z', accessStatus: 'Active' }
      }]);
      if (path.endsWith('/participation/invitations')) return reply([]);
      if (path === '/api/institutions') {
        assert.equal(authorizationProduct(request), 'coaching', 'source institution reads use a temporary coaching token');
        const query = new URL(request.url()).searchParams;
        assert.equal(query.get('pageNumber'), '1');
        assert.equal(query.get('pageSize'), '100');
        assert.equal(query.get('isActive'), 'true');
        return reply({ items: [{ id: sourceInstitutionId, name: 'Kaynak Akademi', type: 'PrivateCourse', isActive: true }],
          totalCount: 1, pageNumber: 1, pageSize: 100 });
      }
      if (path === `/api/coaching-admin/institutions/${sourceInstitutionId}/teachers`) {
        assert.equal(authorizationProduct(request), 'coaching', 'source teacher reads use a temporary coaching token');
        const query = new URL(request.url()).searchParams;
        assert.equal(query.get('pageNumber'), '1');
        assert.equal(query.get('pageSize'), '100');
        teacherRosterRequests++;
        return reply({ teachers: [{ userId: legacyTeacherId, firstName: 'Deniz', lastName: 'Öğretmen',
          email: 'deniz@example.test' }], totalCount: 1 });
      }
      if (path === `/api/course-management/v1/workspaces/${workspaceId}/members` && request.method() === 'GET')
        return reply([{ userId: studentId, role: 'Owner', isActive: true }]);
      if (path === `/api/course-management/v1/workspaces/${workspaceId}/invitations` && request.method() === 'GET')
        return reply(invitations);
      if (path === `/api/course-management/v1/workspaces/${workspaceId}/invitations` && request.method() === 'POST') {
        invitationBody = request.postDataJSON();
        assert.equal(invitationBody.targetUserId, legacyTeacherId);
        assert.equal(invitationBody.role, 2);
        assert.equal(invitationBody.studentRecordId, null);
        assert.deepEqual(Object.keys(invitationBody).sort(), ['id', 'role', 'studentRecordId', 'targetUserId']);
        const invitation = { ...invitationBody, expiresAt: new Date(Date.now() + 7 * 86400000).toISOString(), status: 'Pending' };
        invitations.push(invitation);
        return reply(invitation, 201);
      }
      if (path === `/api/course-management/v1/workspaces/${workspaceId}/imports/legacy-institution/preview`
        && request.method() === 'POST') {
        institutionPreviewBody = request.postDataJSON();
        assert.deepEqual(institutionPreviewBody, { sourceSystem: 'coaching', sourceInstitutionId,
          selectedFields: ['name', 'type'], name: 'Kaynak Akademi', institutionCategory: 'PrivateCourse' });
        return reply({ previewId: '4bc0df19-c606-4a43-9d21-d60c6f55c5da', sourceSystem: 'coaching',
          selectedFields: ['name', 'type'], workspaceName: workspace.name, workspaceInstitutionCategory: null,
          targetName: 'Kaynak Akademi', targetInstitutionCategory: 'PrivateCourse' });
      }
      if (path === `/api/course-management/v1/workspaces/${workspaceId}/imports/legacy-institution`
        && request.method() === 'POST') {
        institutionCommitBody = request.postDataJSON();
        assert.equal(institutionCommitBody.previewId, '4bc0df19-c606-4a43-9d21-d60c6f55c5da');
        assert.equal(institutionCommitBody.expectedWorkspaceName, 'Mevcut Kurs Kurumu');
        assert.equal(institutionCommitBody.expectedWorkspaceCategory, null);
        workspace = { ...workspace, name: 'Kaynak Akademi', institutionCategory: 'PrivateCourse' };
        return reply({ previewId: institutionCommitBody.previewId, name: workspace.name,
          institutionCategory: workspace.institutionCategory, alreadyApplied: false });
      }
      if (path === `/api/course-management/v1/workspaces` && request.method() === 'POST') {
        workspaceCreateRequests++;
        return reply({ code: 'UnexpectedWorkspaceCreate' }, 500);
      }
      if (path.endsWith('/audit') && request.method() === 'GET') return reply([]);
      if (path.endsWith('/students/search')) return reply({ total: 0, skip: 0, take: 25, items: [] });
      if (path.endsWith('/course-management/v1/features')) return reply({ studentPortal: false, assignmentAttachments: false });
      if (path.startsWith(`/api/course-management/v1/workspaces/${workspaceId}/`) && request.method() === 'GET')
        return reply([]);
      // The panel asks which switched features are on (21.11.2026); both stay off here.
      if (path === '/api/course-management/v1/features') return reply({ studentPortal: false, assignmentAttachments: false });
      throw new Error(`Unexpected Course Management import request: ${request.method()} ${path}`);
    });

    await page.goto(`${base}/?product=course-management`);
    await page.getByLabel('E-posta adresi').fill('owner@example.test');
    await page.getByLabel('Şifre', { exact: true }).fill('test-password');
    await page.getByRole('button', { name: 'Personel olarak giriş yap' }).click();
    // The earlier screen is gone (21.11.2026): moving records from another product is under Öğrenciler.
    assert.equal(await page.getByRole('link', { name: 'Önceki ekran' }).count(), 0);
    await page.getByRole('link', { name: 'Öğrenciler' }).click();
    await page.getByRole('button', { name: 'Başka üründen aktar' }).click();
    const legacy = page.locator('section[aria-labelledby="legacy-title"]');
    await legacy.getByRole('tab', { name: 'Öğretmenler' }).click();
    await legacy.getByRole('button', { name: 'Kurumları getir' }).click();
    await legacy.getByLabel('Hangi kurumun öğretmenleri?').selectOption(sourceInstitutionId);
    await legacy.getByLabel(/Deniz Öğretmen/).check();
    await legacy.getByRole('button', { name: '1 öğretmeni davet et' }).click();
    await legacy.getByText('1 öğretmene davet gönderildi.', { exact: true }).waitFor();
    assert.equal(teacherRosterRequests, 1);
    assert.equal(invitationBody.targetUserId, legacyTeacherId);
    assert.equal(invitations[0].status, 'Pending');

    await legacy.getByRole('tab', { name: 'Kurum bilgisi' }).click();
    await legacy.getByRole('button', { name: 'Kurumları getir' }).click();
    await legacy.getByLabel('Hangi kurum?').selectOption(sourceInstitutionId);
    await legacy.getByLabel(/Kurumun adı: Kaynak Akademi/).check();
    await legacy.getByLabel(/Kurumun türü/).check();
    await legacy.getByRole('button', { name: 'Önizle', exact: true }).click();
    await legacy.getByRole('button', { name: 'Aktar', exact: true }).click();
    await legacy.getByText('Kurum bilgileri aktarıldı.', { exact: true }).waitFor();

    assert.ok(institutionPreviewBody);
    assert.ok(institutionCommitBody);
    assert.equal(workspace.name, 'Kaynak Akademi');
    assert.equal(workspace.type, 'Institution');
    assert.equal(workspaceCreateRequests, 0, 'institution details update the existing workspace without creating another one');
  }, '/staff/');
});

test('institution panel: student filters stay in the address, a year-long series is added from the calendar menu, e-mails need consent', async () => {
  await withPage('clients/admin-panel/dist/staff-portal/browser', async (page, base) => {
    const courseId = '1c1d6f0c-5a43-4d44-9d47-6a3a8d1f2b10';
    const groupId = '2d2e7a1d-6b54-4e55-8e58-7b4b9e2a3c21';
    const workspace = { id: workspaceId, name: 'TEST Deneme Kursu', type: 'Institution', institutionCategory: null, role: 'Owner', isReadOnly: false };
    const searches = [];
    const seriesBodies = [];
    let savedEmailSettings = null;
    const kinds = ['Absence', 'HomeworkNotDone', 'InstallmentReminder', 'InstallmentOverdue', 'LessonChanged', 'WeeklySchedule', 'PaymentReceived'];
    const emailSettings = () => ({ rules: kinds.map((kind, index) => ({ kind, kindValue: index + 1,
      enabled: savedEmailSettings?.rules.find(rule => rule.kind === index + 1)?.enabled ?? false, audience: 1, note: null })),
      installmentDaysBefore: 3, installmentOnDueDay: false, overdueDaysAfter: 3, sendHour: 10, replyToEmail: null,
      informedConsentAt: savedEmailSettings?.informedConsentConfirmed ? new Date().toISOString() : null });

    await page.route('**/api/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname;
      const reply = (json, status = 200) => route.fulfill({ status, json });
      if (path.endsWith('/auth/refresh-token')) return reply({ code: 'Auth.InvalidToken' }, 400);
      if (path.endsWith('/auth/course-management/refresh-token'))
        return reply({ accessToken: jwt('course-management'), expiresInMinutes: 15, tokenType: 'Bearer' });
      if (path.endsWith('/auth/captcha-config')) return reply({ enabled: false, siteKey: null });
      if (path.endsWith('/auth/course-management/login')) return reply({ accessToken: jwt('course-management'), expiresInMinutes: 15 });
      if (path.endsWith('/auth/staff-session/products')) return reply([{ product: 'course-management', roles: ['CourseInstitutionOwner'] }]);
      if (path === '/api/course-management/notifications') return reply([]);
      if (path.endsWith('/me/profile')) return reply({ userId: studentId, displayName: 'TEST Kurum Sahibi' });
      if (path.endsWith('/me/memberships')) return reply([workspace]);
      if (path.endsWith('/me/subscriptions')) return reply([]);
      if (path.endsWith('/participation/invitations')) return reply([]);
      if (path.endsWith('/overview')) return reply({ today: new Date().toISOString().slice(0, 10), role: 'Owner', todayLessons: [],
        lessonsWaitingForAttendance: [], finance: { overdueCount: 1, overdueStudentCount: 1, overdueAmount: 400, monthCollections: 1200 },
        emails: { sent: 0, skipped: 0, byKind: [] }, supportAccess: { pendingRequest: false, active: false } });
      if (path.endsWith('/members')) return reply([{ userId: studentId, role: 'Owner', isActive: true, displayName: 'TEST Kurum Sahibi' }]);
      if (path.endsWith('/courses')) return reply([{ id: courseId, name: 'Matematik', subject: 'Matematik' }]);
      if (path.endsWith('/groups')) return reply([{ id: groupId, courseId, courseName: 'Matematik', name: '8. sınıf', teacherUserId: studentId }]);
      if (path.endsWith('/students/search')) {
        searches.push(Object.fromEntries(url.searchParams));
        const items = [{ id: inviteId, displayName: 'Ada Öğrenci', email: null, phoneNumber: null, isActive: true, emailsMuted: false,
          createdAt: '2026-11-01T09:00:00Z', groups: [{ id: groupId, name: '8. sınıf', courseName: 'Matematik' }], guardianCount: 1,
          guardianWithoutEmail: false, overdueAmount: 400 }];
        return reply({ total: 1, skip: 0, take: Number(url.searchParams.get('take') ?? 25), items });
      }
      if (path.endsWith('/lesson-series') && request.method() === 'POST') {
        const body = request.postDataJSON();
        seriesBodies.push(body);
        if (body.preview) return reply({ sessionCount: 50, conflictCount: 1, occurrences: [{ startsAt: `${body.startsOn}T07:00:00Z`,
          endsAt: `${body.startsOn}T08:00:00Z`, conflicts: [{ code: 'Lesson.TeacherConflict', with: 'Fizik' }] }] });
        return reply({ seriesId: body.id, sessionsCreated: 49, sessionsSkipped: 1 }, 201);
      }
      // The setup guide (20.11.2026): one step open, so the guide opens by itself on entry.
      if (path.endsWith('/setup') && request.method() === 'GET') return reply({ name: workspace.name, institutionCategory: null, closed: false,
        steps: ['profile', 'teachers', 'rooms', 'courses', 'students', 'fees', 'schedule', 'emails']
          .map(id => ({ id, status: id === 'profile' ? 'open' : id === 'rooms' ? 'skipped' : 'done' })),
        counts: { teachers: 0, invited: 0, rooms: 0, courses: 1, groups: 1, students: 1, unplaced: 0, withoutFee: 0, futureLessons: 0 } });
      if (path.endsWith('/email-settings') && request.method() === 'PUT') { savedEmailSettings = request.postDataJSON(); return reply(emailSettings()); }
      if (path.endsWith('/email-settings')) return reply(emailSettings());
      if (path.endsWith('/emails')) return reply([]);
      if (path.startsWith(`/api/course-management/v1/workspaces/${workspaceId}/`) && request.method() === 'GET') return reply([]);
      // The panel asks which switched features are on (21.11.2026); both stay off here.
      if (path === '/api/course-management/v1/features') return reply({ studentPortal: false, assignmentAttachments: false });
      throw new Error(`Unexpected institution panel request: ${request.method()} ${path}`);
    });

    await page.goto(`${base}/?product=course-management`);
    await page.getByLabel('E-posta adresi').fill('owner@example.test');
    await page.getByLabel('Şifre', { exact: true }).fill('test-password');
    await page.getByRole('button', { name: 'Personel olarak giriş yap' }).click();
    // The setup guide opens by itself at the first open step; the grouped menu shows its progress on top.
    await page.getByRole('heading', { name: 'Kurumunuzu tanıyalım' }).waitFor();
    await page.getByRole('link', { name: 'Kurulum (7/8)' }).waitFor();
    // On a 390 px phone the menu is one scrolling row and the group headings are hidden; they are still in the page.
    assert.deepEqual(await page.getByRole('navigation', { name: 'maxkampüs' }).locator('.menu-group').allTextContents(), ['Günlük', 'Kurum', 'Diğer']);
    assert.equal(await page.getByRole('button', { name: 'Kurs / dershane' }).getAttribute('aria-pressed'), 'false');
    await page.getByRole('button', { name: 'Daha sonra devam et' }).click();
    await page.getByRole('heading', { name: 'Genel bakış' }).waitFor();
    await page.getByRole('button', { name: 'Kuruluma devam et' }).waitFor();

    // Students: the search and filters are kept in the address, so a refresh keeps them.
    await page.getByRole('link', { name: 'Öğrenciler' }).click();
    await page.getByRole('heading', { name: 'Öğrenciler' }).waitFor();
    await page.getByLabel('Ara').fill('Ada');
    await page.getByLabel('Ara').press('Enter');
    // On a phone the list is cards (21.11.2026 B design); the hidden table holds the same names.
    await page.locator('text=Ada Öğrenci >> visible=true').first().waitFor();
    await page.waitForFunction(() => location.hash.startsWith('#ogrenciler') && location.hash.includes('q=Ada'));
    assert.equal(searches.at(-1).q, 'Ada');
    await page.reload();
    await page.getByRole('heading', { name: 'Öğrenciler' }).waitFor();
    await page.waitForFunction(() => document.querySelector('input[name="q"]')?.value === 'Ada');

    // Calendar: right-click an empty hour, add a lesson series that runs a year, see the clash, skip it and save.
    await page.getByRole('link', { name: 'Takvim' }).click();
    await page.getByRole('heading', { name: 'Takvim' }).waitFor();
    // 22.11.2026: on a Sunday after 10.00 every 10.00 of the current week is a past hour ("geçmiş saat"); the next week
    // always has free future hours, so the test no longer depends on the day it runs.
    await page.getByRole('button', { name: 'Sonraki hafta' }).click();
    await page.getByRole('button', { name: /10\.00, ders ekle$/ }).first().click({ button: 'right' });
    await page.getByRole('menuitem', { name: 'Tekrarlayan ders ekle' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Grup').selectOption({ label: '8. sınıf' });
    await dialog.getByLabel('Ne kadar sürsün').selectOption('year');
    await dialog.getByRole('button', { name: 'Dersleri göster' }).click();
    await dialog.getByText('50 ders oluşacak.').waitFor();
    assert.equal(seriesBodies[0].preview, true);
    const span = (Date.parse(seriesBodies[0].endsOn) - Date.parse(seriesBodies[0].startsOn)) / 86400000;
    assert.ok(span >= 360 && span <= 366, `a year-long series (${span} days)`);
    assert.equal(seriesBodies[0].groupId, groupId);
    assert.equal(seriesBodies[0].startTime, '10:00');
    await dialog.getByLabel('Çakışan 1 dersi atla, kalanları kaydet').check();
    await dialog.getByRole('button', { name: 'Kaydet' }).click();
    await page.getByText('49 ders eklendi; çakışan 1 ders atlandı.').waitFor();
    assert.equal(seriesBodies[1].skipConflicts, true);
    assert.equal(seriesBodies[1].preview, undefined);

    // E-mails: turning one on needs the institution's confirmation that students and guardians were told.
    await page.getByRole('link', { name: 'Ayarlar' }).click();
    await page.getByRole('heading', { name: 'Ayarlar' }).waitFor();
    await page.getByRole('switch', { name: 'Devamsızlık' }).click();
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await page.getByText('E-postaları açmadan önce bilgilendirme onayını işaretleyin.').waitFor();
    assert.equal(savedEmailSettings, null);
    await page.getByLabel(/Öğrencileri ve velileri bilgilendirdik/).check();
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await page.getByText('E-posta ayarları kaydedildi.').waitFor();
    assert.equal(savedEmailSettings.informedConsentConfirmed, true);
    assert.equal(savedEmailSettings.rules.find(rule => rule.kind === 1).enabled, true);
    assert.equal(savedEmailSettings.rules.filter(rule => rule.enabled).length, 1);
  }, '/staff/');
});

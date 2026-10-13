import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken, planningStudent, planningTeacher, planningManager, planningInstitution } from '../support/coaching-planning-session.mjs';

const api = 'http://127.0.0.1:5006/api/coaching/staff/students';
const student = { userId: planningStudent, firstName: 'Ada', lastName: 'Test', fullName: 'Ada Test', email: 'ada@example.invalid', gradeLevel: 8,
  teacherUserId: planningTeacher, teacherName: 'Test Koç', institutionName: 'Test Kurumu', assignmentStartDate: '2026-10-01T00:00:00Z' };

async function staffSession(page, user, role) {
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: planningToken(user, [role]), tokenType: 'Bearer', expiresInMinutes: 15 } }));
  await page.route('**/api/auth/staff-session/products', route => route.fulfill({ json: [{ product: 'coaching', roles: [role] }] }));
  await page.route('**/api/teachers/me/students?*', route => route.fulfill({ json: { items: [student], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 } }));
  await page.route(`**/api/coaching-admin/institutions/${planningInstitution}/students?*`, route => route.fulfill({ json: { students: [student], totalCount: 1 } }));
  await page.route(`**/api/coaching-admin/institutions/${planningInstitution}/teachers?*`, route => route.fulfill({ json: { teachers: [{ userId: planningTeacher, firstName: 'Test', lastName: 'Koç', email: 'coach@example.invalid' }], totalCount: 1 } }));
  await page.goto('http://127.0.0.1:4301/?product=coaching');
}

test('teacher publishes a weekly plan, student completes work, teacher and institution read the result', async ({ browser, request }) => {
  const teacherHeaders = { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` };
  const initialReport = await request.get(`${api}/${planningStudent}/study/report?fromDate=2026-10-19&toDate=2026-10-25`, { headers: teacherHeaders });
  expect(initialReport.ok(), await initialReport.text()).toBeTruthy();
  const initialActualMinutes = (await initialReport.json()).data.actualMinutes ?? 0;
  const drafts = await request.get(`${api}/${planningStudent}/study/plans?status=Draft`, { headers: teacherHeaders });
  expect(drafts.ok(), await drafts.text()).toBeTruthy();
  for (const draft of (await drafts.json()).data.items) {
    expect((await request.post(`${api}/${planningStudent}/study/plans/${draft.id}/archive`, { headers: teacherHeaders, data: { expectedVersion: draft.version } })).ok()).toBeTruthy();
  }
  const teacherContext = await browser.newContext();
  const teacherPage = await teacherContext.newPage();
  await staffSession(teacherPage, planningTeacher, 'Teacher');
  await teacherPage.getByTestId('open-student-report').click();
  await teacherPage.getByRole('button', { name: 'Haftalık plan ve çalışma raporu', exact: true }).click();
  const teacherEditor = teacherPage.locator('app-student-study-plans');
  await teacherEditor.getByRole('button', { name: 'Yeni taslak', exact: true }).click();
  const title = `Koç haftası ${randomUUID()}`;
  await teacherEditor.getByLabel('Plan başlığı', { exact: true }).fill(title);
  await teacherEditor.getByLabel('Çalışma başlığı', { exact: true }).fill('Kesirler tekrarı');
  await teacherEditor.getByLabel('Tarih', { exact: true }).fill('2026-10-19');
  await teacherEditor.getByLabel('Süre (dk)', { exact: true }).fill('45');
  await teacherEditor.getByRole('button', { name: 'Taslağı kaydet', exact: true }).click();
  await teacherEditor.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.').check();
  await teacherEditor.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await expect(teacherEditor.getByText('Öğrencinin çalışma kaydı bekleniyor.')).toBeVisible();
  await expect(teacherEditor.getByLabel('Gerçek süre (dk)', { exact: true })).toHaveCount(0);

  const studentContext = await browser.newContext();
  const studentPage = await studentContext.newPage();
  await studentPage.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: planningToken(planningStudent), tokenType: 'Bearer', expiresInMinutes: 15 } }));
  await studentPage.goto('http://127.0.0.1:4300/coaching-portal/study-plans');
  await studentPage.getByRole('button', { name: new RegExp(title) }).click();
  await studentPage.getByLabel('Gerçek süre (dk)', { exact: true }).fill('40');
  await studentPage.getByRole('button', { name: 'Tamamla', exact: true }).click();
  await expect(studentPage.getByText(/Tamamlandı · 40 dk/)).toBeVisible();

  await teacherEditor.getByRole('button', { name: new RegExp(title) }).click();
  await expect(teacherEditor.getByText(/Tamamlandı · 40 dk/)).toBeVisible();
  const teacherReport = teacherPage.locator('app-student-study-report');
  await teacherReport.getByLabel('Başlangıç').fill('2026-10-19');
  await teacherReport.getByLabel('Bitiş').fill('2026-10-25');
  await teacherReport.getByRole('checkbox', { name: /Önceki haftayla karşılaştır/ }).check();
  await teacherReport.getByRole('button', { name: 'Raporu göster' }).click();
  await expect(teacherReport.getByText(`${initialActualMinutes + 40} dakika`, { exact: true })).toBeVisible();
  await expect(teacherReport.getByText('Haftalar arası çalışma karşılaştırması', { exact: true })).toBeVisible();
  await teacherPage.getByTestId('open-session-brief').click();
  await expect(teacherEditor).toHaveCount(0);
  await expect(teacherReport.getByText('Haftalar arası çalışma karşılaştırması', { exact: true })).toBeVisible();

  const institutionContext = await browser.newContext();
  const institutionPage = await institutionContext.newPage();
  await staffSession(institutionPage, planningManager, 'InstitutionAdmin');
  await institutionPage.getByTestId('institution-students-tab').click();
  await institutionPage.getByTestId(`student-report-${planningStudent}`).click();
  await institutionPage.getByRole('button', { name: 'Haftalık plan ve çalışma raporu', exact: true }).click();
  const institutionEditor = institutionPage.locator('app-student-study-plans');
  await institutionEditor.getByRole('button', { name: new RegExp(title) }).click();
  await expect(institutionEditor.getByText(/Tamamlandı · 40 dk/)).toBeVisible();
  await expect(institutionEditor.getByRole('button', { name: 'Yeni taslak' })).toHaveCount(0);
  await expect(institutionEditor.getByRole('button', { name: 'Tamamla', exact: true })).toHaveCount(0);
  const institutionReport = institutionPage.locator('app-student-study-report');
  await institutionReport.getByLabel('Başlangıç').fill('2026-10-19');
  await institutionReport.getByLabel('Bitiş').fill('2026-10-25');
  await institutionReport.getByRole('checkbox', { name: /Önceki haftayla karşılaştır/ }).check();
  await institutionReport.getByRole('button', { name: 'Raporu göster' }).click();
  await expect(institutionReport.getByText(`${initialActualMinutes + 40} dakika`, { exact: true })).toBeVisible();
  await expect(institutionReport.getByText('Haftalar arası çalışma karşılaştırması', { exact: true })).toBeVisible();
  await institutionPage.setViewportSize({ width: 390, height: 844 });
  expect(await institutionPage.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await institutionPage.screenshot({ path: '../../artifacts/coaching-completion-20261013/institution-plan-proof.png', fullPage: true });
  await institutionPage.getByTestId('open-session-brief').click();
  await expect(institutionEditor).toHaveCount(0);
  await expect(institutionReport.getByText('Haftalar arası çalışma karşılaştırması', { exact: true })).toBeVisible();
  expect(await institutionPage.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await Promise.all([teacherContext.close(), studentContext.close(), institutionContext.close()]);
});

test('teacher score targets survive title editing and stay read-only for student and institution', async ({ browser, request }) => {
  const context = await browser.newContext(); const page = await context.newPage();
  await staffSession(page, planningTeacher, 'Teacher');
  await page.getByTestId('goals-tab').click();
  const goals = page.locator('staff-coaching-teacher-goals');
  const title = `Puan hedefi ${randomUUID()}`;
  await goals.getByRole('combobox', { name: /^Öğrenci/ }).selectOption(planningStudent);
  await goals.getByLabel('Hedef başlığı', { exact: true }).fill(title);
  await goals.getByRole('button', { name: 'Hedef oluştur', exact: true }).click();
  let card = goals.locator('article').filter({ hasText: title });
  const goalId = await card.getByRole('button', { name: 'Düzenle', exact: true }).getAttribute('data-testid');
  const id = goalId.replace('edit-goal-', '');
  const initialTarget = await request.get(`${api}/${planningStudent}/study/goals/${id}/target`, { headers: { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` } });
  expect(initialTarget.ok(), await initialTarget.text()).toBeTruthy();
  const oldVersion = (await initialTarget.json()).data.version;
  const teacherList = await request.get(`http://127.0.0.1:5006/api/goals/teacher/${planningTeacher}?pageNumber=1&pageSize=25`, { headers: { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` } });
  expect(teacherList.ok(), await teacherList.text()).toBeTruthy();
  expect((await teacherList.json()).items.find(goal => goal.id === id)?.version).toBe(oldVersion);
  await card.getByRole('button', { name: 'Okul / üniversite hedefini görüntüle' }).click();
  const editor = card.locator('app-student-goal-target');
  await editor.getByRole('combobox', { name: /^Sınav türü/ }).selectOption('LGS');
  await editor.getByLabel('Hedef puan', { exact: true }).fill('450');
  await editor.getByLabel('En yüksek puan (ölçek)', { exact: true }).fill('500');
  await editor.getByRole('button', { name: 'Puan hedefini kaydet' }).click();
  await expect(editor.getByText(/Puan hedefi kaydedildi/)).toBeVisible();
  const staleEdit = await request.put(`http://127.0.0.1:5006/api/goals/${id}`, { headers: { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` },
    data: { goalId: id, title: 'Stale goal form', description: null, category: 'ExamPreparation', targetDate: null,
      targetScore: null, targetExamType: null, targetSubject: null, expectedVersion: oldVersion } });
  expect(staleEdit.status(), await staleEdit.text()).toBe(409);
  await card.getByRole('button', { name: 'Düzenle', exact: true }).click();
  await goals.getByLabel('Hedef başlığı', { exact: true }).fill(`${title} güncel`);
  await goals.getByRole('button', { name: 'Hedefi güncelle', exact: true }).click();
  await expect(goals.getByRole('heading', { name: `${title} güncel`, exact: true })).toBeVisible();
  for (const [url, user, role, canEdit] of [
    [`${api}/${planningStudent}/study/goals/${id}/target`, planningTeacher, 'Teacher', true],
    [`${api}/${planningStudent}/study/goals/${id}/target`, planningManager, 'InstitutionAdmin', false],
    [`http://127.0.0.1:5006/api/coaching/study-planning/goals/${id}/target`, planningStudent, 'Student', false]
  ]) {
    const response = await request.get(url, { headers: { Authorization: `Bearer ${planningToken(user, [role])}` } });
    expect(response.ok(), await response.text()).toBeTruthy();
    const data = (await response.json()).data;
    expect(data.scoreTarget).toMatchObject({ targetScore: 450, maxScore: 500, examType: 'LGS' });
    expect(data.canEdit).toBe(canEdit);
  }
  await page.screenshot({ path: '../../artifacts/coaching-completion-20261013/teacher-target-proof.png', fullPage: true });
  await context.close();
});

test('real staff endpoints reject foreign students, student role and institution writes', async ({ request }) => {
  const target = `${api}/${planningStudent}/study/plans`;
  expect((await request.get(target)).status()).toBe(401);
  expect((await request.get(target, { headers: { Authorization: `Bearer ${planningToken(planningStudent)}` } })).status()).toBe(403);
  expect((await request.get(`${api}/${randomUUID()}/study/plans`, { headers: { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` } })).status()).toBe(403);
  expect((await request.post(target, { headers: { Authorization: `Bearer ${planningToken(planningManager, ['InstitutionAdmin'])}` },
    data: { title: 'Forbidden institution write', tasks: [] } })).status()).toBe(403);
});

test('institution reads real comparison and warnings then opens the scoped student report', async ({ browser, request }) => {
  const context = await browser.newContext(); const page = await context.newPage();
  await staffSession(page, planningManager, 'InstitutionAdmin');
  await page.getByTestId('institution-analytics-tab').click();
  const analytics = page.locator('staff-coaching-institution-analytics');
  await expect(page.getByRole('heading', { name: 'Kurum öğretmenleri', exact: true })).toHaveCount(0);
  await analytics.getByLabel('Başlangıç tarihi').fill('2026-10-01');
  await analytics.getByLabel('Bitiş tarihi').fill('2026-10-13');
  await analytics.getByRole('combobox', { name: /^Sınıf/ }).selectOption({ label: '8. sınıf' });
  await analytics.getByRole('button', { name: 'Raporu göster', exact: true }).click();
  await expect(analytics.getByRole('cell', { name: 'Ada Test', exact: true })).toBeVisible();
  await analytics.getByRole('button', { name: 'Öğrenci raporu', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Ada Test', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Haftalık plan ve çalışma raporu', exact: true }).click();
  await expect(page.locator('app-student-study-plans').getByRole('heading', { name: 'Öğrencinin çalışma planları' })).toBeVisible();
  await page.getByTestId('back-to-roster').click();
  await expect(analytics.getByLabel('Başlangıç tarihi')).toHaveValue('2026-10-01');
  await expect(analytics.getByLabel('Bitiş tarihi')).toHaveValue('2026-10-13');
  await expect(analytics.getByRole('cell', { name: 'Ada Test', exact: true })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await expect(analytics.getByRole('heading', { name: 'Kurum analizi ve erken uyarılar' })).toBeVisible();
  await page.screenshot({ path: '../../artifacts/coaching-completion-20261013/institution-analytics-mobile-proof.png', fullPage: true });
  const response = await request.get(`http://127.0.0.1:5006/api/reports/institution/${randomUUID()}/comparison`, {
    headers: { Authorization: `Bearer ${planningToken(planningManager, ['InstitutionAdmin'])}` }
  });
  expect(response.status()).toBe(403);
  await context.close();
});

test('institution edits and removes members through scoped Identity contracts with pending-save guards', async ({ browser }) => {
  const context = await browser.newContext(); const page = await context.newPage();
  await staffSession(page, planningManager, 'InstitutionAdmin');
  const replacementTeacher = randomUUID();
  let members = [{ ...student }];
  let teachers = [
    { userId: planningTeacher, firstName: 'Test', lastName: 'Koç', email: 'coach@example.invalid' },
    { userId: replacementTeacher, firstName: 'Yeni', lastName: 'Koç', email: 'new@example.invalid' }
  ];
  let updateBody; let removalCount = 0; let releaseSave;
  const saving = new Promise(resolve => { releaseSave = resolve; });
  await page.route(`**/api/coaching-admin/institutions/${planningInstitution}/students?*`, route => route.fulfill({ json: { students: members, totalCount: members.length } }));
  await page.route(`**/api/coaching-admin/institutions/${planningInstitution}/teachers?*`, route => route.fulfill({ json: { teachers, totalCount: teachers.length } }));
  await page.route(`**/api/institution/students/${planningStudent}`, async route => {
    if (route.request().method() === 'PUT') {
      updateBody = route.request().postDataJSON();
      await saving;
      members = [{ ...student, gradeLevel: updateBody.gradeLevel, teacherUserId: updateBody.teacherUserId, teacherName: 'Yeni Koç' }];
    } else if (route.request().method() === 'DELETE') { removalCount++; members = []; }
    else throw new Error('Unexpected student membership method');
    await route.fulfill({ status: 204 });
  });
  await page.route(`**/api/institution/teachers/${replacementTeacher}`, async route => {
    expect(route.request().method()).toBe('DELETE');
    teachers = teachers.filter(teacher => teacher.userId !== replacementTeacher);
    await route.fulfill({ status: 204 });
  });
  await page.getByTestId('institution-students-tab').click();
  await page.getByTestId(`edit-student-${planningStudent}`).click();
  const editor = page.locator('.member-editor');
  await editor.getByLabel('Öğrencinin sınıfı', { exact: true }).fill('9');
  await editor.getByRole('combobox', { name: /^Atanacak öğretmen/ }).selectOption(replacementTeacher);
  await editor.getByRole('button', { name: 'Değişikliği kaydet' }).click();
  await expect.poll(() => updateBody).toEqual({ gradeLevel: 9, teacherUserId: replacementTeacher });
  await expect(page.getByRole('searchbox', { name: 'Öğrenci ara', exact: true })).toBeDisabled();
  await expect(editor.getByRole('button', { name: 'Değişikliği kaydet' })).toBeDisabled();
  await page.getByRole('button', { name: 'Güvenli çıkış' }).click();
  await expect(page.getByText('Üye işlemi sürüyor. Tamamlanmasını bekleyin.')).toBeVisible();
  releaseSave();
  await expect(editor).toHaveCount(0);
  const row = page.getByRole('row').filter({ hasText: 'Ada Test' });
  await expect(row).toContainText('9. sınıf'); await expect(row).toContainText('Yeni Koç');
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByTestId(`remove-student-${planningStudent}`).click();
  expect(removalCount).toBe(0);
  page.once('dialog', dialog => dialog.accept());
  await page.getByTestId(`remove-student-${planningStudent}`).click();
  await expect(row).toHaveCount(0); expect(removalCount).toBe(1);
  await page.getByTestId('institution-teachers-tab').click();
  page.once('dialog', dialog => dialog.accept());
  await page.getByTestId(`remove-teacher-${replacementTeacher}`).click();
  await expect(page.getByTestId(`remove-teacher-${replacementTeacher}`)).toHaveCount(0);
  await page.getByTestId('institution-students-tab').click();
  await expect(page.getByTestId('institution-student-invite-teacher').locator(`option[value="${replacementTeacher}"]`)).toHaveCount(0);
  await context.close();
});

test('institution assigns and suspends a real subscription seat and student access follows it', async ({ browser, request }) => {
  const base = 'http://127.0.0.1:5006';
  const adminHeaders = { Authorization: `Bearer ${planningToken(randomUUID(), ['SystemAdmin'])}` };
  const plan = await request.post(`${base}/api/coaching-admin/subscriptions/plans`, { headers: adminHeaders, data: {
    slug: `institution-e2e-${randomUUID()}`, name: 'E2E kurum kontenjanı', description: 'Disposable local test', audience: 'Institution', price: 1000,
    isContactOnly: false, billingPeriod: 'Annual', durationDays: 365, includedStudentSeats: 1, features: [], isActive: true, isPublic: false, sortOrder: 0
  } });
  expect(plan.ok(), await plan.text()).toBeTruthy();
  const planId = (await plan.json()).data.id;
  const license = await request.post(`${base}/api/coaching-admin/subscriptions`, { headers: adminHeaders, data: {
    planId, institutionId: planningInstitution, studentIds: [], startDate: new Date().toISOString(), notes: 'Disposable E2E'
  } });
  expect(license.ok(), await license.text()).toBeTruthy();
  const subscriptionId = (await license.json()).data.id;
  const context = await browser.newContext(); const page = await context.newPage();
  try {
    await staffSession(page, planningManager, 'InstitutionAdmin');
    await page.getByTestId('institution-subscription-tab').click();
    const panel = page.locator('staff-coaching-institution-subscription');
    await expect(panel.getByText('E2E kurum kontenjanı', { exact: true })).toBeVisible();
    await panel.getByTestId(`institution-seat-${planningStudent}`).click();
    await expect(panel.getByTestId(`institution-seat-${planningStudent}`)).toHaveText('Kontenjanı kaldır');
    const access = await request.get(`${base}/api/coaching/subscriptions/my-access`, { headers: { Authorization: `Bearer ${planningToken(planningStudent)}` } });
    expect(access.ok(), await access.text()).toBeTruthy();
    expect((await access.json()).data.subscriptions.map(item => item.id)).toContain(subscriptionId);
    await panel.getByTestId(`institution-seat-${planningStudent}`).click();
    await expect(panel.getByTestId(`institution-seat-${planningStudent}`)).toHaveText('Koltuk ata');
    const removed = await request.get(`${base}/api/coaching/subscriptions/my-access`, { headers: { Authorization: `Bearer ${planningToken(planningStudent)}` } });
    expect((await removed.json()).data.subscriptions.map(item => item.id)).not.toContain(subscriptionId);
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
    await page.screenshot({ path: '../../artifacts/coaching-completion-20261013/institution-subscription-proof.png', fullPage: true });
    const forbidden = await request.put(`${base}/api/coaching/institution-subscription/${subscriptionId}/students/${randomUUID()}`, {
      headers: { Authorization: `Bearer ${planningToken(planningManager, ['InstitutionAdmin'])}` }, data: { isSuspended: false }
    });
    expect(forbidden.status()).toBe(403);
  } finally {
    await context.close();
    expect((await request.put(`${base}/api/coaching-admin/subscriptions/${subscriptionId}`, { headers: adminHeaders, data: { status: 'Cancelled', endDate: null, notes: 'Disposable test complete' } })).ok()).toBeTruthy();
  }
});

test('admin goal form rejects a stale version and explicitly reloads the current score scale', async ({ browser, request }) => {
  const base = 'http://127.0.0.1:5006';
  const teacherHeaders = { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` };
  const adminToken = planningToken(randomUUID(), ['SystemAdmin']); const adminHeaders = { Authorization: `Bearer ${adminToken}` };
  const created = await request.post(`${base}/api/goals`, { headers: { ...teacherHeaders, 'Idempotency-Key': randomUUID() }, data: {
    studentId: planningStudent, teacherId: planningTeacher, title: `Admin sürüm testi ${randomUUID()}`, category: 'ExamPreparation', targetScore: 400
  } });
  expect(created.ok(), await created.text()).toBeTruthy(); const id = (await created.json()).goalId;
  const context = await browser.newContext(); const page = await context.newPage();
  try {
    await page.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: adminToken, tokenType: 'Bearer', expiresInMinutes: 15 } }));
    await page.goto(`http://127.0.0.1:4300/dashboard/coaching/operations/goal/${id}/edit`);
    const form = page.locator('app-coaching-resource-edit');
    await expect(form.getByLabel('Hedef puan', { exact: true })).toHaveValue('400');
    const detail = await request.get(`${base}/api/coaching-admin/goals/${id}`, { headers: adminHeaders });
    expect(detail.ok(), await detail.text()).toBeTruthy(); const oldVersion = (await detail.json()).version;
    const score = await request.put(`${api}/${planningStudent}/study/goals/${id}/target/score`, { headers: teacherHeaders,
      data: { expectedVersion: oldVersion, targetScore: 450, maxScore: 500, examType: 'LGS' } });
    expect(score.ok(), await score.text()).toBeTruthy();
    await form.getByLabel('Başlık', { exact: true }).fill('Admin eski form');
    await form.getByRole('button', { name: 'Kaydet', exact: true }).click();
    await expect(form.getByTestId('reload-admin-goal')).toBeVisible();
    await expect(form.getByLabel('Başlık', { exact: true })).toHaveValue('Admin eski form');
    await form.getByTestId('reload-admin-goal').click();
    await expect(form.getByLabel('Hedef puan', { exact: true })).toHaveValue('450');
    await form.getByLabel('Başlık', { exact: true }).fill('Admin güncel form');
    await form.getByRole('button', { name: 'Kaydet', exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard\/coaching\/operations$/);
    const target = await request.get(`${api}/${planningStudent}/study/goals/${id}/target`, { headers: teacherHeaders });
    expect((await target.json()).data.scoreTarget).toMatchObject({ targetScore: 450, maxScore: 500, examType: 'LGS' });
    const current = await request.get(`${base}/api/coaching-admin/goals/${id}`, { headers: adminHeaders });
    expect((await current.json()).title).toBe('Admin güncel form');
  } finally {
    await context.close();
    expect((await request.delete(`${base}/api/goals/${id}`, { headers: teacherHeaders })).ok()).toBeTruthy();
  }
});

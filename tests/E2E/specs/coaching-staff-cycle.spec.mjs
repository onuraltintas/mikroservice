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
  await teacherReport.getByRole('button', { name: 'Raporu göster' }).click();
  await expect(teacherReport.getByText(`${initialActualMinutes + 40} dakika`, { exact: true })).toBeVisible();

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
  await institutionPage.screenshot({ path: '../../artifacts/coaching-completion-20261013/institution-plan-proof.png', fullPage: true });
  await Promise.all([teacherContext.close(), studentContext.close(), institutionContext.close()]);
});

test('real staff endpoints reject foreign students, student role and institution writes', async ({ request }) => {
  const target = `${api}/${planningStudent}/study/plans`;
  expect((await request.get(target)).status()).toBe(401);
  expect((await request.get(target, { headers: { Authorization: `Bearer ${planningToken(planningStudent)}` } })).status()).toBe(403);
  expect((await request.get(`${api}/${randomUUID()}/study/plans`, { headers: { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` } })).status()).toBe(403);
  expect((await request.post(target, { headers: { Authorization: `Bearer ${planningToken(planningManager, ['InstitutionAdmin'])}` },
    data: { title: 'Forbidden institution write', tasks: [] } })).status()).toBe(403);
});

import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken } from '../support/coaching-planning-session.mjs';

const api = 'http://127.0.0.1:5006/api';
const adminToken = () => planningToken(randomUUID(), ['SystemAdmin']);
const files = {
  'lessons.json': '[{"id":"l","name":"Browser Lesson","gradeName":"TYT"}]',
  'units-derived.json': '[{"id":"u","name":"Unit","lessonId":"l","displayOrder":null}]',
  'upper-subjects.json': '[{"id":"p","name":"Parent","unitId":"u","lessonId":"l","rank":1}]',
  'subjects.json': '[{"id":"t","name":"Topic","unitId":"u","upperSubjectId":"p","rank":2}]',
  'university-programs.json': '[{"id":"p1","university":"University","programName":"Program","programCode":"123","scoreType":"SAY","minScore":400,"scoreYear":2025}]',
  'lgs-programs.json': '[{"id":"s1","schoolName":"School","city":"City","town":"District","minScore":350}]'
};
async function session(page, token) {
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: token, tokenType: 'Bearer', expiresInMinutes: 15 } }));
}

test('real admin APIs reject anonymous/student access and stale preview', async ({ request }) => {
  const url = `${api}/coaching-admin/catalog-imports/preview`;
  const data = { source: `browser-${randomUUID()}`, files };
  expect((await request.post(url, { data })).status()).toBe(401);
  expect((await request.post(url, { data, headers: { Authorization: `Bearer ${planningToken()}` } })).status()).toBe(403);
  const headers = { Authorization: `Bearer ${adminToken()}` };
  const reviewed = await request.post(url, { data, headers });
  expect(reviewed.ok(), await reviewed.text()).toBeTruthy();
  const review = (await reviewed.json()).data;
  expect(review.newRecords).toBe(6);
  const approved = await request.post(`${api}/coaching-admin/catalog-imports/approve`, { headers, data: { ...data, fingerprint: review.fingerprint, reason: 'Browser test approval' } });
  expect(approved.ok(), await approved.text()).toBeTruthy();
  const stale = await request.post(`${api}/coaching-admin/catalog-imports/publish`, { headers, data: { ...data, fingerprint: review.fingerprint, reason: 'Stale preview must fail' } });
  expect(stale.status()).toBe(409);
  expect((await request.get(`${api}/coaching-admin/students/${randomUUID()}/study/plans`, { headers: { Authorization: `Bearer ${planningToken()}` } })).status()).toBe(403);
});

test('admin browser previews, imports inactive, reviews again and publishes', async ({ page }) => {
  await session(page, adminToken());
  await page.goto('/dashboard/coaching/catalog');
  await page.getByText('Toplu katalog aktarımı ve yayın', { exact: true }).click();
  const form = page.locator('app-coaching-catalog-import');
  await form.getByLabel('Kaynak adı').fill(`browser-ui-${randomUUID()}`);
  await form.locator('input[type=file]').setInputFiles(Object.entries(files).map(([name, value]) => ({ name, mimeType: 'application/json', buffer: Buffer.from(value) })));
  await form.getByRole('button', { name: 'Önizle ve doğrula' }).click();
  await expect(form.getByText(/6 yeni, 0 mevcut/)).toBeVisible();
  await expect(form.getByRole('button', { name: 'Doğrulanmış kaynağı yayınla' })).toBeDisabled();
  await form.getByLabel('İşlem gerekçesi').fill('Tarayıcı pasif aktarım testi');
  await form.getByRole('button', { name: 'Yeni kayıtları pasif aktar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Onayla', exact: true }).click();
  await expect(form.getByLabel('İşlem gerekçesi')).toHaveCount(0);
  await form.getByRole('button', { name: 'Önizle ve doğrula' }).click();
  await expect(form.getByText(/0 yeni, 6 mevcut/)).toBeVisible();
  await form.getByLabel('İşlem gerekçesi').fill('Tarayıcı ayrı yayın onayı');
  await form.getByRole('button', { name: 'Doğrulanmış kaynağı yayınla' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Onayla', exact: true }).click();
  await expect(form.getByLabel('İşlem gerekçesi')).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(form.getByRole('heading', { name: 'Kontrollü katalog aktarımı' })).toBeVisible();
  await page.screenshot({ path: '../../artifacts/local-admin-catalog-e2e/catalog-mobile.png', fullPage: true });
});

test('admin inspects real student revisions and report without student write controls', async ({ page }) => {
  const student = randomUUID(); const institution = randomUUID(); const token = adminToken();
  const headers = { Authorization: `Bearer ${planningToken(student)}` };
  const created = await page.request.post(`${api}/coaching/study-planning/plans`, { headers, data: { title: 'Browser inspection plan', tasks: [{ plannedDate: '2026-10-02', title: 'Inspection task', plannedMinutes: 30, topicId: null, isPinned: false }] } });
  expect(created.ok(), await created.text()).toBeTruthy();
  await session(page, token);
  // Existing Identity roster/profile contracts are isolated. New study endpoints use the real Coaching database.
  await page.route('**/api/institutions?*', route => route.fulfill({ json: { items: [{ id: institution, name: 'Fixture institution' }], totalCount: 1 } }));
  await page.route(`**/api/coaching-admin/institutions/${institution}/students?*`, route => route.fulfill({ json: { students: [{ userId: student, firstName: 'Fixture', lastName: 'Student', email: 'fixture@example.invalid', gradeLevel: 10 }], totalCount: 1 } }));
  await page.route(`**/api/coaching-admin/students/${student}/detail`, route => route.fulfill({ json: { studentId: student, totalAssignments: 0, totalExams: 0, totalSessions: 0, totalGoals: 0, assignments: [], exams: [] } }));
  await page.route(`**/api/coaching-admin/students/${student}/history?*`, route => route.fulfill({ json: { items: [], totalCount: 0 } }));
  await page.goto(`/dashboard/coaching/students/${student}?institutionId=${institution}`);
  const panel = page.locator('app-coaching-admin-study');
  await expect(panel.getByRole('button', { name: /Browser inspection plan revizyon/ })).toBeVisible();
  await panel.getByRole('button', { name: /Browser inspection plan revizyon/ }).click();
  await expect(panel.getByText(/Inspection task/)).toBeVisible();
  const report = panel.locator('app-student-study-report');
  await report.getByLabel('Başlangıç', { exact: true }).fill('2026-10-02');
  await report.getByLabel('Bitiş', { exact: true }).fill('2026-10-02');
  await report.getByRole('button', { name: 'Raporu göster' }).click();
  await expect(report.getByText('Bu döneme planlanmış çalışma yok. Başarı oranı hesaplanmadı.', { exact: true })).toBeVisible();
  await expect(report.getByRole('heading', { name: 'Öğrencinin güncel hedefleri' })).toBeVisible();
  await expect(panel.locator('button[type=submit]')).toHaveCount(0);
  await panel.getByRole('button', { name: 'Düzeltme ve işlem geçmişini aç' }).click();
  const corrections=panel.locator('app-coaching-study-corrections');
  await corrections.getByLabel('İşlem gerekçesi').fill('Browser reviewed plan correction');
  await corrections.getByLabel('Düzeltilmiş plan başlığı').fill('Reviewed browser plan');
  await corrections.getByRole('button',{name:'Plan başlığını düzelt'}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Onayla',exact:true}).click();
  await expect(corrections.getByLabel('Düzeltilmiş plan başlığı')).toHaveValue('Reviewed browser plan');
  await corrections.getByRole('button',{name:'İşlem geçmişini yükle'}).click();
  await expect(corrections.getByText(/Browser reviewed plan correction/)).toBeVisible();
  await corrections.getByLabel('İşlem gerekçesi').fill('Browser archive reviewed plan');
  await corrections.getByRole('button',{name:'Planı arşivle'}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Onayla',exact:true}).click();
  await expect(corrections.getByLabel('Düzeltilmiş plan başlığı')).toHaveCount(0);
  await page.screenshot({ path: '../../artifacts/local-admin-catalog-e2e/student-review.png', fullPage: true });
});

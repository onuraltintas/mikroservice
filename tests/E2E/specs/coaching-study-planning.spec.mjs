import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken } from '../support/coaching-planning-session.mjs';

test('real API enforces authentication, product scope, role and student ownership', async ({ request }) => {
  const url = 'http://127.0.0.1:5006/api/coaching/study-planning/plans';
  expect((await request.get(url)).status()).toBe(401);
  expect((await request.get(url, { headers: { Authorization: `Bearer ${planningToken(randomUUID(), ['Teacher'])}` } })).status()).toBe(403);
  expect((await request.get(url, { headers: { Authorization: `Bearer ${planningToken(randomUUID(), ['Student'], 'speed-reading')}` } })).status()).toBe(403);
  const owner = planningToken(randomUUID()); const headers = { Authorization: `Bearer ${owner}` };
  const created = await request.post(url, { headers, data: { title: 'Ownership test', tasks: [
    { plannedDate: '2026-10-02', title: 'Review', plannedMinutes: 30, topicId: null, isPinned: false }
  ] } });
  expect(created.ok(), await created.text()).toBeTruthy();
  const id = (await created.json()).data.id;
  const other = await request.get(`${url}/${id}`, { headers: { Authorization: `Bearer ${planningToken(randomUUID())}` } });
  expect(other.status()).toBe(404);
});

test('student creates, publishes, completes a plan and enters a self-reported exam', async ({ page }) => {
  const student = randomUUID();
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ accessToken: planningToken(student), tokenType: 'Bearer', expiresInMinutes: 15 }) }));
  const headers = { Authorization: `Bearer ${planningToken(student)}`, 'Idempotency-Key': randomUUID() };
  const goalTitle = `Browser goal ${randomUUID()}`;
  const goalResponse = await page.request.post('http://127.0.0.1:5006/api/goals', { headers, data: {
    studentId: student, title: goalTitle, category: 'ExamPreparation', teacherId: null, description: null,
    targetDate: '2027-06-01T00:00:00Z', targetScore: 400
  } });
  expect(goalResponse.ok(), await goalResponse.text()).toBeTruthy();
  const goalId = (await goalResponse.json()).goalId;
  expect((await page.request.put(`http://127.0.0.1:5006/api/goals/${goalId}/progress`, {
    headers, data: { goalId, progress: 30 }
  })).ok()).toBeTruthy();
  await page.goto('/coaching-portal/progress');
  const goalPicker = page.locator('app-student-goal-target');
  await goalPicker.getByRole('button', { name: 'Okul / üniversite hedefini görüntüle' }).click();
  await goalPicker.getByRole('combobox', { name: 'Sınav türü', exact: true }).selectOption('Mock');
  await goalPicker.getByLabel('Hedef puan', { exact: true }).fill('90');
  await goalPicker.getByLabel('En yüksek puan (ölçek)', { exact: true }).fill('100');
  await goalPicker.getByRole('button', { name: 'Puan hedefini kaydet', exact: true }).click();
  await expect(goalPicker.getByText(/Puan hedefi kaydedildi/)).toBeVisible();
  await page.goto('/coaching-portal/study-plans');
  await expect(page.getByRole('heading', { name: 'Çalışma planlarım', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Yeni taslak', exact: true }).click();
  const title = `Browser plan ${randomUUID()}`;
  await page.getByLabel('Plan başlığı', { exact: true }).fill(title);
  await page.getByLabel('Çalışma başlığı', { exact: true }).fill('Review fractions');
  await page.getByLabel('Tarih', { exact: true }).fill('2026-10-02');
  await page.getByLabel('Süre (dk)', { exact: true }).fill('30');
  await page.getByRole('button', { name: 'Taslağı kaydet', exact: true }).click();
  await expect(page.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.')).toBeEnabled();
  await page.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.').check();
  await page.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await expect(page.getByLabel('Gerçek süre (dk)', { exact: true })).toBeVisible();
  await page.getByLabel('Gerçek süre (dk)', { exact: true }).fill('25');
  await page.getByRole('button', { name: 'Tamamla', exact: true }).click();
  await expect(page.getByText(/Tamamlandı · 25 dk/)).toBeVisible();
  await page.goto('/coaching-portal/exam-results');
  await page.getByLabel('Başlık', { exact: true }).fill(`Browser exam ${randomUUID()}`);
  await page.getByLabel('Tarih', { exact: true }).fill('2026-10-02');
  await page.getByLabel('Aldığın puan', { exact: true }).fill('80');
  await page.getByLabel('Doğru', { exact: true }).fill('8');
  await page.getByLabel('Yanlış', { exact: true }).fill('1');
  await page.getByLabel('Boş', { exact: true }).fill('1');
  await page.getByLabel('Bilgilerimi kontrol ettim; öğrenci beyanı olarak kaydet.').check();
  await page.getByRole('button', { name: 'Sonucu kaydet', exact: true }).click();
  await expect(page.getByText('Sonucun kaydedildi. Öğrenci beyanı olarak raporlanacak.')).toBeVisible();
  const report = await page.request.get('http://127.0.0.1:5006/api/coaching/study-planning/reports?fromDate=2026-10-02&toDate=2026-10-02', {
    headers: { Authorization: `Bearer ${planningToken(student)}` }
  });
  expect(report.ok()).toBeTruthy();
  const data = (await report.json()).data;
  expect(data.completedTasks).toBe(1); expect(data.actualMinutes).toBe(25);
  expect(data.examGroups.some(x => x.source === 'StudentReported' && x.averagePercentage === 80)).toBeTruthy();
  expect(data.goals).toHaveLength(1);
  expect(data.goals[0]).toMatchObject({ goalId, recordedProgress: 30, source: 'Unspecified' });
  expect(data.goals[0].scoreAssessment.comparisons[0]).toMatchObject({ source: 'StudentReported', targetAttainmentPercentage: 88.9, remainingScore: 10 });
  await page.goto('/coaching-portal/progress');
  const studyReport = page.locator('app-student-study-report');
  await studyReport.getByLabel('Başlangıç', { exact: true }).fill('2026-10-02');
  await studyReport.getByLabel('Bitiş', { exact: true }).fill('2026-10-02');
  await studyReport.getByRole('button', { name: 'Raporu göster', exact: true }).click();
  await expect(studyReport.getByText('Tamamlanan çalışma', { exact: true })).toBeVisible();
  await expect(studyReport.getByText(/Ortalama %80/)).toBeVisible();
  await expect(studyReport.getByText(goalTitle, { exact: true })).toBeVisible();
  await expect(studyReport.getByText(/Kaydedilen ilerleme: %30/)).toBeVisible();
  await expect(studyReport.getByText(/Puan hedefine erişim: %88.9/)).toBeVisible();
});

test('student links a school target and publishes an automatic topic plan within saved capacity', async ({ page }) => {
  const student = randomUUID();
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ accessToken: planningToken(student), tokenType: 'Bearer', expiresInMinutes: 15 }) }));
  const headers = { Authorization: `Bearer ${planningToken(student)}`, 'Idempotency-Key': randomUUID() };
  const created = await page.request.post('http://127.0.0.1:5006/api/goals', { headers, data: {
    studentId: student, title: `Catalog goal ${randomUUID()}`, category: 'ExamPreparation', teacherId: null,
    description: null, targetDate: '2027-06-01T00:00:00Z', targetScore: null
  } });
  expect(created.ok(), await created.text()).toBeTruthy();
  const goalId = (await created.json()).goalId;
  await page.goto('/coaching-portal/progress');
  const picker = page.locator('app-student-goal-target');
  await picker.getByRole('button', { name: 'Okul / üniversite hedefini görüntüle' }).click();
  await picker.getByLabel('Okul adı', { exact: true }).fill('E2E Okul');
  await picker.getByRole('button', { name: 'Ara', exact: true }).click();
  await expect(picker.getByText('E2E Okul', { exact: true })).toBeVisible();
  await picker.getByRole('button', { name: 'Bu hedefi bağla', exact: true }).click();
  await expect(picker.getByText('Hedef bağlantısı kaydedildi.', { exact: true })).toBeVisible();
  const target = await page.request.get(`http://127.0.0.1:5006/api/coaching/study-planning/goals/${goalId}/target`, { headers });
  expect((await target.json()).data.catalogTarget.name).toBe('E2E Okul');
  await page.goto('/coaching-portal/study-availability');
  await page.getByRole('button', { name: 'Saat aralığı ekle', exact: true }).click();
  await page.getByRole('combobox', { name: 'Gün', exact: true }).selectOption('Friday');
  await page.getByLabel('Başlangıç', { exact: true }).fill('09:00');
  await page.getByLabel('Bitiş', { exact: true }).fill('10:00');
  await page.getByRole('button', { name: 'Çalışma saatlerini kaydet', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Yeniden yükle', exact: true })).toBeEnabled();
  await page.goto('/coaching-portal/automatic-plan');
  await page.getByLabel('Konu, ders veya ünite', { exact: true }).fill('E2E Kesirler');
  await page.getByRole('combobox', { name: 'Sınıf', exact: true }).selectOption({ label: '8' });
  await page.getByRole('button', { name: 'Konuları ara', exact: true }).click();
  await page.getByRole('button', { name: 'Konu ekle', exact: true }).click();
  await page.getByLabel('E2E Kesirler çalışma süresi', { exact: true }).fill('30');
  await page.getByLabel('Başlangıç tarihi', { exact: true }).fill('2026-10-02');
  await page.getByLabel('Plan süresi (gün)', { exact: true }).fill('1');
  await page.getByRole('button', { name: 'Önizleme oluştur', exact: true }).click();
  await expect(page.getByText(/Yeni çalışmalar: 30 dk/)).toBeVisible();
  const title = `Automatic browser ${randomUUID()}`;
  await page.getByLabel('Plan başlığı', { exact: true }).fill(title);
  await page.getByLabel('Önizlemeyi kontrol ettim; taslak olarak kaydetmeyi onaylıyorum.').check();
  await page.getByRole('button', { name: 'Taslağı kaydet', exact: true }).click();
  await expect(page.getByText(`${title} taslak olarak kaydedildi. Henüz yayımlanmadı.`, { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Çalışma planlarımı aç', exact: true }).click();
  await page.getByRole('button', { name: new RegExp(title) }).click();
  await page.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.').check();
  await page.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'E2E Kesirler', exact: true })).toBeVisible();
  const report = await page.request.get('http://127.0.0.1:5006/api/coaching/study-planning/reports?fromDate=2026-10-02&toDate=2026-10-02', { headers });
  const data = (await report.json()).data;
  expect(data).toMatchObject({ scheduledTasks: 1, completedTasks: 0, plannedMinutes: 30, actualMinutes: null });
  expect(data.topics[0].topicName).toBe('E2E Kesirler');
  expect(data.goals[0].scoreAssessment).toMatchObject({ reason: 'ScoreTargetNotConfigured', comparisons: [] });
});

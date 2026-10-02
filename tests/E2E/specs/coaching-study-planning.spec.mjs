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
    headers: { Authorization: `Bearer ${planningToken()}` }
  });
  expect(report.ok()).toBeTruthy();
  const data = (await report.json()).data;
  expect(data.completedTasks).toBeGreaterThan(0); expect(data.actualMinutes).toBeGreaterThanOrEqual(25);
  expect(data.examGroups.some(x => x.source === 'StudentReported' && x.averagePercentage === 80)).toBeTruthy();
});

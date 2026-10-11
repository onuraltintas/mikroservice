import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

const exerciseId = '30000000-0000-0000-0000-000000000001', typeId = '30000000-0000-0000-0000-000000000002';
async function prepare(page, configuration, role = 'Teacher') {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Sakkad testi', difficultyLevel: 1,
    exerciseTypeName: 'Saccade', configurationJson: JSON.stringify(configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Saccade', displayName: 'Sakkad', engineType: 'motion_path', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Sakkad', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  return { errors, writes };
}

for (const age of ['child', 'teen', 'young-adult', 'adult']) {
  const catalog = JSON.parse(readFileSync(new URL(`../../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
  for (const level of [1, 5]) test(`${age} level ${level} opens with approved interval and completes after active time only`, async ({ page }) => {
    const config = catalog.find(item => item.type === 'Saccade' && item.difficultyLevel === level).configuration;
    const { errors, writes } = await prepare(page, config);
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Hedef geçiş aralığı (ms)', { exact: true })).toHaveValue(String(config.engineConfig.timing.holdMs));
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    await expect(page.getByText(/tıklamanız veya cevap yazmanız gerekmez/)).toBeVisible();
    await page.clock.runFor(500);
    await page.getByRole('button', { name: 'Duraklat', exact: true }).click();
    const count = await page.locator('.target-counter').textContent();
    await page.clock.runFor(10000);
    await expect(page.locator('.target-counter')).toHaveText(count);
    await page.getByRole('button', { name: 'Devam et', exact: true }).click();
    await page.clock.runFor(config.engineConfig.timing.durationSeconds * 1000);
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
    await expect(page.locator('.saccade-results')).toContainText('Hedef geçiş aralığı');
    await expect(page.locator('.saccade-results')).toContainText('Tamamlanan hedef geçişi');
    const seconds = await page.locator('.saccade-results .stat-card').filter({ hasText: 'Aktif çalışma süresi' }).locator('.stat-value').textContent();
    expect(Number.parseFloat(seconds.replace(',', '.'))).toBeCloseTo(config.engineConfig.timing.durationSeconds, 0);
    await expect(page.getByText(/harf yanıtları sunucuda doğrulanmadığı/)).toHaveCount(0);
    expect(errors).toEqual([]); expect(writes).toEqual([]);
  });
}

test('custom interval is validated and used without altering the catalog or saving a preview', async ({ page }, testInfo) => {
  const { errors, writes } = await prepare(page, { engineType: 'motion_path', engineConfig: {
    mode: 'saccade', content: { type: 'number', pattern: 'horizontal' }, timing: { holdMs: 800, durationSeconds: 5 } } }, 'Admin');
  const dialog = page.getByRole('dialog'), interval = dialog.getByLabel('Hedef geçiş aralığı (ms)', { exact: true });
  await interval.fill('0'); await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(dialog.getByRole('alert')).toBeVisible();
  await interval.fill('500'); await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await page.clock.runFor(499); await expect(page.locator('.target-counter')).toHaveText('0 geçiş');
  await page.clock.runFor(1); await expect(page.locator('.target-counter')).toHaveText('1 geçiş');
  await page.clock.runFor(4600);
  await expect(page.locator('.saccade-results .stat-card').filter({ hasText: 'Hedef geçiş aralığı' }).locator('.stat-value')).toHaveText(/0[.,]50 sn/);
  await expect(page.locator('.measurement-not-available')).toContainText('Göz hareketleri ölçülmedi');
  await page.locator('.saccade-results').scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('saccade-result.png'), animations: 'disabled' });
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});

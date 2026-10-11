import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

// Actual frontend/engine with disposable Identity/API fixtures; no production writes.
const exerciseId = '30000000-0000-0000-0000-000000000001';
const typeId = '30000000-0000-0000-0000-000000000002';
async function prepare(page, configuration, role = 'Teacher') {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Sabitleme testi', difficultyLevel: 1,
    exerciseTypeName: 'Fixation', configurationJson: JSON.stringify(configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Fixation', displayName: 'Sabitleme', engineType: 'motion_path', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Sabitleme', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  return { errors, writes };
}

for (const age of ['child', 'teen', 'young-adult', 'adult']) {
  const catalog = JSON.parse(readFileSync(new URL(`../../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
  for (const level of [1, 5]) test(`${age} level ${level} preserves safe custom letter and round controls`, async ({ page }) => {
    const configuration = catalog.find(item => item.type === 'Fixation' && item.difficultyLevel === level).configuration;
    const { errors, writes } = await prepare(page, configuration);
    const config = configuration.engineConfig, dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Tur sayısı', { exact: true })).toHaveValue(String(config.content.points));
    const letters = dialog.getByLabel('Her turdaki harf sayısı', { exact: true });
    await expect(letters).toHaveValue(String(config.content.peripheralCount));
    await expect(letters).toHaveAttribute('max', String(config.content.maxPeripheralCount));
    await letters.fill(String(config.content.maxPeripheralCount + 1));
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(dialog.getByRole('alert')).toBeVisible();
    await expect(page.locator('.start-button-large')).toHaveCount(0);
    await letters.fill(String(config.content.peripheralCount));
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click(); await page.clock.runFor(350);
    const stimuli = page.locator('.peripheral-char');
    await expect(stimuli).toHaveCount(config.content.peripheralCount);
    await expect(page.getByText(`0 / ${config.content.points} Tur`, { exact: true })).toBeVisible();
    await page.clock.runFor(config.timing.holdMs);
    await expect(stimuli).toHaveCount(0);
    await expect(page.getByRole('textbox', { name: 'Gördüğünüz harfler' })).toBeVisible();
    expect(errors).toEqual([]); expect(writes).toEqual([]);
  });
}

test('four-letter custom preview scores complete rounds, pauses safely and shows seconds without saving', async ({ page }, testInfo) => {
  const { errors, writes } = await prepare(page, { engineType: 'motion_path', engineConfig: {
    mode: 'fixation', content: { points: 2, peripheralCount: 4, maxPeripheralCount: 4 }, timing: { holdMs: 100 } } }, 'Admin');
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await expect(page.locator('.motion-path-stats')).toBeVisible();
  await expect(page.locator('.motion-path-stats')).toContainText('Gösterim');
  await expect(page.locator('.motion-path-stats')).toContainText('Doğru');
  await expect(page.locator('.motion-path-stats')).toContainText('Yanlış');
  await expect(page.locator('.motion-path-stats')).toContainText('0.10 sn');
  let previous = [];
  for (let round = 0; round < 2; round++) {
    await page.clock.runFor(350);
    const stimuli = page.locator('.peripheral-char');
    const answers = (await stimuli.allTextContents()).map(value => value.trim());
    expect(answers).toHaveLength(4); expect(new Set(answers).size).toBe(4);
    expect(answers.filter(value => previous.includes(value))).toEqual([]); previous = answers;
    await page.clock.runFor(100);
    if (round === 0) {
      await page.getByRole('button', { name: 'Duraklat', exact: true }).click();
      await page.clock.runFor(5000);
      await page.getByRole('button', { name: 'Devam et', exact: true }).click();
    }
    const input = page.getByRole('textbox', { name: 'Gördüğünüz harfler' });
    await input.fill(round === 0 ? answers.reverse().join('') : 'AAAA');
    await page.clock.runFor(20);
    const bounds = await input.boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(page.viewportSize().width);
    await page.getByRole('button', { name: /Gönder/ }).click();
    await expect(page.locator('.feedback-text')).toHaveText(round === 0 ? 'Doğru!' : 'Yanlış!');
    await page.clock.runFor(1200);
  }
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  const completed = page.locator('.stat-card').filter({ hasText: 'Tamamlanan / planlanan tur' });
  await expect(completed.locator('.stat-value')).toHaveText('2 / 2');
  await expect(page.locator('.stat-card').filter({ hasText: 'Ortalama cevap süresi' }).locator('.stat-value')).toHaveText(/\d+[.,]\d{2} sn/);
  await expect(page.locator('.stat-card').filter({ hasText: 'Doğru yanıt' }).locator('.stat-value')).toHaveText('1');
  await expect(page.locator('.stat-card').filter({ hasText: 'Yanlış yanıt' }).locator('.stat-value')).toHaveText('1');
  await expect(page.locator('.stat-card').filter({ hasText: 'Doğruluk' }).locator('.stat-value')).toHaveText('50%');
  await expect(page.getByText(/Egzersiz tamamlandı ve kaydedildi/)).toHaveCount(0);
  await page.clock.runFor(5000);
  await completed.scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('fixation-result.png'), animations: 'disabled' });
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});

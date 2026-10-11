import { test, expect } from '@playwright/test';
test('tracking starts after validation and reports no measured gaze or WPM', async ({ page }) => {
  const exerciseId = '76000000-0000-4000-8000-000000000001';
  const typeId = '76000000-0000-4000-8000-000000000002';
  let starts = 0, completed = 0; const errors = [];
  const configuration = { engineType: 'motion_path', engineConfig: { mode: 'tracking',
    path: { type: 'circle' }, target: { type: 'arrow', color: '#ff0000', size: 'large' }, timing: { durationSeconds: 5, speedMs: 1250 } } };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname; let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'EyeTracking', title: 'Göz Takibi', difficultyLevel: 1, configurationJson: JSON.stringify(configuration) };
    else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'EyeTracking', displayName: 'Göz Takibi', engineType: 'motion_path', isActive: true }] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'tracking-test', configuration, initialData: { engineType: 'motion_path', totalSteps: 5 } };
    else if (path.endsWith('/validate')) { expect(route.request().postDataJSON().action).toBe('tracking_start'); starts++; response = { isValid: true }; }
    else if (path.endsWith('/tracking-test/complete')) { completed++; response = { measurementStatus: 'NotMeasured', rawWPM: null, wordsRead: null, score: null, accuracy: null, xpGained: 0, detailedResults: {} }; }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`);
  await expect(page.getByText('Göz hareketleri veya okuma hızı ölçülmez.', { exact: false })).toBeVisible();
  await page.locator('.start-button-large').click();
  await expect(page.locator('.tracking-instruction')).toContainText('Gözlerinizle noktayı takip edin');
  await expect(page.locator('.target-dot')).toHaveCSS('width', '48px');
  await expect(page.locator('.target-dot')).toHaveCSS('background-color', 'rgb(255, 0, 0)');
  await expect(page.locator('.target-dot')).toContainText('➜');
  await expect.poll(() => completed, { timeout: 15000 }).toBe(1);
  await expect(page.getByText('Göz hareketleri ölçülmedi', { exact: false })).toBeVisible();
  expect(starts).toBe(1); expect(errors).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
});

// Local UI/engine is real; Identity/API fixtures never receive a preview write.
for (const variant of ['recorded', 'custom', 'jump']) {
  test(`tracking ${variant} preview preserves the chosen cycle and paused time`, async ({ page }) => {
    const exerciseId = '76000000-0000-4000-8000-000000000001';
    const typeId = '76000000-0000-4000-8000-000000000002';
    const role = variant === 'custom' ? 'Teacher' : 'Admin';
    const configuration = { engineType: 'motion_path', engineConfig: { mode: 'tracking',
      path: { type: 'circle' }, content: { type: 'dot', pointSize: 36 }, timing: { durationSeconds: 5, speedMs: 1250 } } };
    const exercise = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'EyeTracking',
      title: 'Göz Takibi', difficultyLevel: 1, configurationJson: JSON.stringify(configuration) };
    const writes = [], errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', roles: [role],
        token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role })).toString('base64url')}.fixture` };
      else if (request.method() !== 'GET') {
        writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected preview write' } });
      } else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
      else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'EyeTracking', displayName: 'Göz Takibi', engineType: 'motion_path', isActive: true }], totalCount: 1 };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      await route.fulfill({ json: response });
    });
    await page.goto('/student/exercises');
    if (variant === 'recorded') await page.getByRole('button', { name: 'Kayıtlı ayarlarla başlat' }).click();
    else {
      await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
      const dialog = page.getByRole('dialog');
      const cycle = dialog.getByLabel('Hareket döngüsü / geçiş aralığı (ms)');
      await expect(cycle).toHaveValue('1250');
      await expect(dialog).toContainText('Düşük süre daha hızlı hareket demektir.');
      await cycle.fill('49');
      await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
      await expect(dialog.getByRole('alert')).toBeVisible();
      await dialog.getByRole('button', { name: 'Varsayılana dön' }).click();
      await expect(cycle).toHaveValue('1250');
      await cycle.fill('2000');
      await dialog.getByLabel('Hareket yolu').selectOption(variant === 'jump' ? 'two_point_jump' : 'horizontal');
      await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    }
    await expect(page.getByText('Göz hareketleri veya okuma hızı ölçülmez.', { exact: false })).toBeVisible();
    const fixed = new Date('2026-10-12T09:00:00Z');
    await page.clock.install({ time: fixed }); await page.clock.pauseAt(fixed);
    await page.locator('.start-button-large').click();
    const target = page.locator('.target-container');
    const left = () => target.evaluate(element => Number.parseFloat(element.style.left));
    if (variant === 'recorded') {
      await page.clock.runFor(625);
      expect(await left()).toBeCloseTo(15, 0);
    } else if (variant === 'custom') {
      await page.clock.runFor(500);
      expect(await left()).toBeCloseTo(50, 0);
    } else {
      const first = await left();
      await page.clock.runFor(1000); expect(await left()).toBe(first);
      await page.clock.runFor(1000); expect(await left()).not.toBe(first);
    }
    await page.getByRole('button', { name: 'Duraklat', exact: true }).click();
    const paused = await left();
    await page.clock.runFor(10000); expect(await left()).toBe(paused);
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toHaveCount(0);
    await page.getByRole('button', { name: 'Devam et', exact: true }).click();
    await page.clock.runFor(5100);
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
    await expect(page.getByText('Göz hareketleri ölçülmedi', { exact: false })).toBeVisible();
    expect(writes).toEqual([]); expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  });
}

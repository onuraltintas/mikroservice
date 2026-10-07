import { test, expect } from '@playwright/test';

test('feedback never displaces fixation or stimulus and counters update', async ({ page }) => {
  const id = '40000000-0000-0000-0000-000000000001';
  const typeId = '40000000-0000-0000-0000-000000000002';
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id, exerciseTypeId: typeId, title: 'Takistoskop', difficultyLevel: 1,
    exerciseTypeName: 'Tachistoscope', configurationJson: JSON.stringify({ engineType: 'text_stream',
      engineConfig: { mode: 'flash', Words: ['bir', 'iki', 'ses'], content: { count: 3 },
        timing: { durationMs: 100, intervalMs: 0 }, adaptive: { enabled: false } } }) };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Admin' };
      response = { id: 'test-user', roles: ['Admin'], token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture` };
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Tachistoscope', displayName: 'Takistoskop', engineType: 'text_stream', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${id}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await expect(page.locator('.fixation-cross')).toBeVisible();
  const center = await page.locator('.fixation-cross').boundingBox();
  await page.clock.runFor(330);
  const word = await page.locator('.stimulus-display').innerText();
  const stimulus = await page.locator('.stimulus-display').boundingBox();
  expect(Math.abs(stimulus.x + stimulus.width / 2 - center.x - center.width / 2)).toBeLessThan(2);
  expect(Math.abs(stimulus.y + stimulus.height / 2 - center.y - center.height / 2)).toBeLessThan(2);
  await page.clock.runFor(600);
  await page.locator('.answer-input').fill(word);
  await page.locator('.submit-btn').click();
  await expect(page.locator('.feedback-display')).toContainText('Doğru!');
  await expect(page.locator('.fixation-cross')).toHaveCount(0);
  await expect(page.locator('.game-stats .stat').filter({ hasText: 'Doğru' })).toContainText('1');
  await expect(page.locator('.game-stats .stat').filter({ hasText: 'Yanlış' })).toContainText('0');
  await page.clock.runFor(600);
  await expect(page.locator('.feedback-display')).toHaveCount(0);
  await expect(page.locator('.fixation-cross')).toBeVisible();
  expect(errors).toEqual([]);
});

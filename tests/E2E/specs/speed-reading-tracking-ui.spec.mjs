import { test, expect } from '@playwright/test';
test('tracking starts after validation and reports no measured gaze or WPM', async ({ page }) => {
  const exerciseId = '76000000-0000-4000-8000-000000000001';
  const typeId = '76000000-0000-4000-8000-000000000002';
  let starts = 0, completed = 0; const errors = [];
  const configuration = { engineType: 'motion_path', engineConfig: { mode: 'tracking',
    path: { type: 'circle' }, timing: { durationSeconds: 5, speedMs: 1250 }, content: { pointSize: 48 } } };
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
  await page.locator('.start-button-large').click();
  await expect(page.locator('.tracking-instruction')).toContainText('Gözlerinizle noktayı takip edin');
  await expect(page.locator('.target-dot')).toHaveCSS('width', '48px');
  await expect.poll(() => completed, { timeout: 15000 }).toBe(1);
  await expect(page.getByText('Göz hareketleri ölçülmedi', { exact: false })).toBeVisible();
  expect(starts).toBe(1); expect(errors).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
});

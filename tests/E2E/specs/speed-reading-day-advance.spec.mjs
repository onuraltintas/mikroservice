import { test, expect } from '@playwright/test';

async function setup(page, { allow = true, complete = true, reject = false, delayState = false } = {}) {
  let day = 1;
  const writes = [];
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'day-advance-test' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = [];
    if (path.endsWith('/auth/refresh-token')) response = { id: 'day-advance-test', roles: ['Student'],
      token: `e30.${Buffer.from(JSON.stringify({ sub: 'day-advance-test', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture` };
    else if (path.endsWith('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.endsWith('/assessment/status')) response = { hasCompleted: true };
    else if (path.endsWith('/day-advance')) {
      if (delayState) await new Promise(resolve => setTimeout(resolve, 300));
      response = { programProgressId: 'program-1', completedProgramDay: 1, nextProgramDay: 2, allowEarlyAdvance: allow, canAdvance: allow && complete && day === 1 };
    } else if (path.endsWith('/advance-day')) {
      writes.push(request.postDataJSON());
      await new Promise(resolve => setTimeout(resolve, 150));
      if (reject) return route.fulfill({ status: 400, json: { detail: 'İlerleme izni kapalı.' } });
      day = 2;
      response = { programProgressId: 'program-1', completedProgramDay: 1, nextProgramDay: 2, allowEarlyAdvance: true, canAdvance: false };
    } else if (path.endsWith('/progress/programs')) response = [{ id: 'program-1', currentWeek: 1, currentDay: day,
      templateName: 'Test programı', currentDifficultyLevel: 1, exercisesCompleted: 2, totalExercisesCompleted: 2,
      currentStreak: 1, longestStreak: 1, averageSuccessRate: 80, totalWeeks: 1, totalDays: 3 }];
    else if (/\/daily-progress\/day\/\d+$/.test(path)) response = [1, 2].map(order => ({ exerciseId: `exercise-${order}`,
      exerciseTypeId: 'type', exerciseTypeName: 'Fixation', title: `Görev ${order}`, difficultyLevel: 1,
      estimatedDurationMinutes: 1, order, isCompleted: day === 1 && (complete || order === 1) }));
    await route.fulfill({ json: response });
  });
  await page.goto('/student/daily-exercises');
  await expect(page.getByRole('heading', { name: 'Günlük İlerleme', exact: true })).toBeVisible();
  return writes;
}

test('completed day opens exactly the next day and retains completed counts', async ({ page }) => {
  const writes = await setup(page, { delayState: true });
  const button = page.getByRole('button', { name: /Sonraki güne geç/ });
  await expect(button).toBeVisible();
  await button.click();
  await expect(page.getByText('Hafta 1 • Gün 2', { exact: true })).toBeVisible();
  expect(writes).toEqual([{ programProgressId: 'program-1', completedProgramDay: 1 }]);
  await expect(button).toHaveCount(0);
  await expect(page.locator('.de-progress-banner__count')).toHaveText('0 / 2');
  await page.reload();
  await expect(page.getByText('Hafta 1 • Gün 2', { exact: true })).toBeVisible();
  await page.screenshot({ path: `artifacts/day-advance-20261015/day-two-${test.info().project.name}.png`, fullPage: true });
});

for (const options of [{ allow: false }, { complete: false }]) test(`no advance button for ${JSON.stringify(options)}`, async ({ page }) => {
  const writes = await setup(page, options);
  await expect(page.locator('.de-progress-banner__count')).toBeVisible();
  await expect(page.getByRole('button', { name: /Sonraki güne geç/ })).toHaveCount(0);
  expect(writes).toEqual([]);
});

test('server rejection keeps the completed day and does not fabricate progress', async ({ page }) => {
  const writes = await setup(page, { reject: true });
  await page.getByRole('button', { name: /Sonraki güne geç/ }).click();
  await expect(page.getByText('Hata: İlerleme izni kapalı.', { exact: true })).toBeVisible();
  await expect(page.getByText('Hafta 1 • Gün 1', { exact: true })).toBeVisible();
  await expect(page.locator('.de-progress-banner__count')).toHaveText('2 / 2');
  expect(writes).toHaveLength(1);
});

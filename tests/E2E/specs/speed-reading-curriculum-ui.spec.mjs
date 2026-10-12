import { test, expect } from '@playwright/test';

test('daily program shows honest configured and response-dependent duration on desktop and mobile', async ({ page }) => {
  const errors = [], writes = [];
  const typeId = '76000000-0000-4000-8000-000000000002';
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', roles: ['Student'],
      token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture` };
    else if (request.method() !== 'GET') { writes.push(path); return route.fulfill({ status: 500, json: {} }); }
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.includes('/assessment') && path.endsWith('/status')) response = { hasCompleted: true };
    else if (path.endsWith('/progress/programs')) response = [{ currentDay: 1, currentWeek: 1, currentDifficultyLevel: 1,
      totalDays: 28, totalWeeks: 4, templateName: 'Güncel V3', exercisesCompleted: 0, totalExercisesCompleted: 0, averageSuccessRate: 0, currentStreak: 0 }];
    else if (path.endsWith('/today-exercises') || path.endsWith('/daily-progress/day/1')) response = [
      { exerciseId: '76000000-0000-4000-8000-000000000003', exerciseTypeId: typeId, exerciseTypeName: 'Göz Takibi',
        title: 'Göz Takibi', description: '', difficultyLevel: 1, estimatedDurationMinutes: 1, isCompleted: false, order: 1 },
      { exerciseId: '76000000-0000-4000-8000-000000000004', exerciseTypeId: typeId, exerciseTypeName: 'Anlama',
        title: 'Anlama çalışması', description: '', difficultyLevel: 1, estimatedDurationMinutes: 0, isCompleted: false, order: 2 }
    ];
    await route.fulfill({ json: response });
  });
  await page.goto('/student/daily-exercises');
  await expect(page.getByText(/~1 dk \(cevaplar hariç\)/)).toBeVisible();
  await expect(page.getByText(/Yanıt sürenize bağlı/)).toBeVisible();
  await expect(page.getByText('~0 dk', { exact: true })).toHaveCount(0);
  expect(writes).toEqual([]); expect(errors).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
});

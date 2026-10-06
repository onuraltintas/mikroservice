import { test, expect } from '@playwright/test';

const exerciseId = '70000000-0000-0000-0000-000000000001';
const typeId = '70000000-0000-0000-0000-000000000002';
async function prepare(page, role, mode) {
  const errors = [], writes = [];
  const configuration = { engineType: 'focus', engineConfig: { mode, nLevel: 1, speedMs: 1000, gridSize: 3,
    totalSteps: 3, positionSequence: [1, 2, 1], wordSequence: ['kitap', 'kalem', 'kitap'] } };
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Odaklanma testi', difficultyLevel: 3,
    exerciseTypeName: 'Focus', configurationJson: JSON.stringify(configuration) };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Focus', displayName: 'Odaklanma', engineType: 'focus', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Odaklanma', exact: true })).toBeVisible();
  return { errors, writes, configuration };
}

for (const mode of ['position', 'word', 'dual']) {
  test(`teacher ${mode} preview applies mode trials grid and pause without writes`, async ({ page }) => {
    const { errors, writes } = await prepare(page, 'Teacher', 'position');
    await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.locator('select').selectOption(mode);
    const inputs = dialog.locator('input[type=number]');
    await inputs.nth(0).fill('500'); await inputs.nth(1).fill('1');
    await inputs.nth(2).fill('4'); await inputs.nth(3).fill('3');
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    await expect(page.locator('.mode-badge')).toBeVisible();
    await expect(page.locator('.focus-grid .grid-cell')).toHaveCount(mode === 'word' ? 0 : 16);
    await expect(page.locator('.current-word-display')).toHaveCount(mode === 'position' ? 0 : 1);
    const pause = page.locator('.mental-registration-container .header-section.right button').first();
    await page.clock.runFor(200); await pause.click(); await page.clock.runFor(2000);
    await expect(page.locator('.interaction-area button').first()).toBeDisabled();
    await pause.click(); await page.clock.runFor(2500);
    await expect(page.getByRole('status').filter({ hasText: /Önizleme sonucu.*kaydedilmedi/ })).toBeVisible();
    expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}

test('student unanswered session uses server completion without WPM', async ({ page }) => {
  const { errors, configuration } = await prepare(page, 'Student', 'position');
  let completed = 0; const actions = [];
  await page.route('**/api/**/daily-progress/complete-exercise', route => route.fulfill({ json: {} }));
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname; let response = {};
    if (path.endsWith('/start')) response = { sessionId: 'owned-focus', exerciseId, totalSteps: 3, status: 'Active', configuration,
      initialData: { engineType: 'focus', focusMode: 'position', focusNLevel: 1, focusSpeedMs: 1000, gridSize: 3, positionSequence: [1, 2, 1], totalSteps: 3 } };
    else if (path.endsWith('/validate')) {
      const action = route.request().postDataJSON(); actions.push(action.action);
      response = { isValid: true, isCorrect: null, isCompleted: action.action === 'complete', feedbackData: { hits: 0, misses: 0, falseAlarms: 0 } };
    } else if (path.endsWith('/complete')) {
      completed++; response = { correctCount: 0, incorrectCount: 0, accuracy: 0, score: 0,
        rawWPM: null, wordsRead: null, comprehensionScore: null, measurementStatus: 'Measured', detailedResults: {} };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click(); await page.clock.runFor(3500);
  await expect.poll(() => completed).toBe(1);
  expect(actions).toEqual(['focus_start', 'complete']);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

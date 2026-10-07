import { test, expect } from '@playwright/test';

// Real UI and engine with deterministic API/Identity fixtures; persistence is checked separately.
const exerciseId = '20000000-0000-0000-0000-000000000001';
const typeId = '20000000-0000-0000-0000-000000000002';
const textId = '20000000-0000-0000-0000-000000000003';
const exercise = {
  id: exerciseId, exerciseTypeId: typeId, title: 'Tarama tarayıcı testi', difficultyLevel: 1,
  configurationJson: JSON.stringify({ engineType: 'scanning', readingTextId: textId,
    engineConfig: { targetCount: 2, timeLimit: 90 } })
};

async function prepare(page, role = 'Admin') {
  const writes = [], errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role], firstName: 'Test' };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: { message: 'Unexpected preview write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Scanning', displayName: 'Tarama', engineType: 'scanning', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1, pageNumber: 1, pageSize: 100 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, title: 'Metin', content: '“IŞIK” [İNCİ] başka', wordCount: 3, questions: [] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises');
  await expect(page.getByRole('heading', { name: 'Tarama', exact: true })).toBeVisible();
  return { writes, errors };
}

async function launch(page, seconds = '90') {
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Süre sınırı (saniye)').fill(seconds);
  await dialog.getByLabel('Hedef sözcük sayısı').fill('3');
  await dialog.getByLabel('Metin boyutu (px)').fill('28');
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.locator('.start-button-large').click();
  await expect(page.locator('.scan-text-area')).toHaveCSS('font-size', '28px');
}

test('custom targets, Turkish words and keyboard completion without preview writes', async ({ page }) => {
  const { writes, errors } = await prepare(page);
  await launch(page);
  await expect(page.locator('.targets-list .target-word')).toHaveCount(3);
  await page.locator('.scan-word').first().focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('.scan-word').first()).toHaveAttribute('aria-pressed', 'true');
  await page.locator('.scan-word').nth(1).click();
  await page.locator('.scan-word').nth(2).click();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

test('teacher pause excludes elapsed time and timeout remains incomplete', async ({ page }) => {
  const { writes, errors } = await prepare(page, 'Teacher');
  await page.clock.install();
  await launch(page, '1');
  const pause = page.locator('.scan-find-container .game-header button').first();
  await pause.click();
  await page.clock.runFor(2000);
  await expect(page.locator('.scan-find-container')).toBeVisible();
  await pause.click();
  await page.clock.runFor(1100);
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

test('student does not see staff preview controls', async ({ page }) => {
  await prepare(page, 'Student');
  await expect(page.getByRole('button', { name: 'Özel ayarlarla dene' })).toHaveCount(0);
});

test('student server snapshot, acknowledgement and pause synchronization', async ({ page }) => {
  const { errors } = await prepare(page, 'Student');
  const sessionId = '20000000-0000-0000-0000-000000000004';
  const actions = [];
  let found = [], pauses = 0, resumes = 0, completions = 0;
  await page.route(`**/api/**/exercises/${exerciseId}`, route => route.fulfill({ json: {
    ...exercise, configurationJson: JSON.stringify({ engineType: 'skimming', engineConfig: { targetCount: 2, timeLimit: 90 } })
  } }));
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response;
    const rounds = () => [{ textContent: '“IŞIK” [İNCİ] başka', targets: ['ışık', 'inci'], foundTargets: [...found] }];
    if (path.endsWith('/start')) response = {
      sessionId, exerciseId, totalSteps: 2, status: 'Active', configuration: { engineType: 'skimming', engineConfig: { timeLimit: 90 } },
      initialData: { engineType: 'skimming', content: '“IŞIK” [İNCİ] başka', scanningRounds: rounds(), currentRound: 0, totalSteps: 2, timeLimitSeconds: 90 }
    };
    else if (path.endsWith('/validate')) {
      const action = route.request().postDataJSON();
      actions.push(action.action);
      if (action.action === 'scan_click') found.push(action.index === 0 ? 'ışık' : 'inci');
      response = { isValid: true, isCompleted: found.length === 2,
        feedbackData: { scanningRounds: rounds(), currentRound: found.length === 2 ? 1 : 0,
          totalSteps: 2, correctCount: found.length, incorrectCount: 0, searchTimeMs: 500 } };
    } else if (path.endsWith('/pause')) { pauses++; response = {}; }
    else if (path.endsWith('/resume')) { resumes++; response = {}; }
    else if (path.endsWith('/complete')) {
      completions++;
      response = { sessionId, correctCount: 2, incorrectCount: 0, accuracy: 100, score: 100,
        xpGained: 0, detailedResults: {}, measurementStatus: 'Measured' };
    } else response = {};
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`);
  await page.locator('.start-button-large').click();
  await expect.poll(() => actions.includes('scan_start')).toBe(true);
  const pause = page.locator('.scan-find-container .game-header button').first();
  await pause.click();
  await expect.poll(() => pauses).toBe(1);
  await pause.click();
  await expect.poll(() => resumes).toBe(1);
  await page.locator('.scan-word').first().click();
  await expect(page.locator('.scan-word').first()).toHaveAttribute('aria-pressed', 'true');
  await page.locator('.scan-word').nth(1).click();
  await expect.poll(() => completions).toBe(1);
  expect(actions).toEqual(['scan_start', 'scan_click', 'scan_click']);
  expect(errors).toEqual([]);
});

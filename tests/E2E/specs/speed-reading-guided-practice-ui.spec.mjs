import { test, expect } from '@playwright/test';

// Real Angular UI/engine; deterministic Identity/API fixtures. PostgreSQL is verified separately.
const exerciseId = '62000000-0000-4000-8000-000000000001';
const typeId = '62000000-0000-4000-8000-000000000002';
const textId = '62000000-0000-4000-8000-000000000003';
const age = '10000000-0000-0000-0000-000000000001';
const text = 'bir iki üç dört beş altı';
const configuration = { engineType: 'word_highlight', engineConfig: { mode: 'speed_reading', readingPurpose: 'practice',
  content: { source: 'reading_text', minWordCount: 5, maxWordCount: 8 }, pacer: { speedWpm: 200, chunkSize: 1 } } };

async function prepare(page, role = 'Admin', missing = false) {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Hızlı Okuma testi', difficultyLevel: 3,
    targetAgeGroupId: age, exerciseTypeName: 'SpeedReading', configurationJson: JSON.stringify(configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      const daily = path.endsWith('/daily-progress/complete-exercise');
      return route.fulfill({ status: daily ? 200 : 500, json: daily ? {} : { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'SpeedReading', displayName: 'Hızlı Okuma', engineType: 'word_highlight', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, title: 'Gerçek metin', content: text, wordCount: 6,
      difficultyLevel: 3, targetAgeGroupConfigurationId: age, questions: [] };
    else if (path.endsWith('/reading-texts')) response = missing ? [] : [{ id: textId, wordCount: 6, difficultyLevel: 3, targetAgeGroupConfigurationId: age, isActive: true }];
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Hızlı Okuma', exact: true })).toBeVisible();
  return { errors, writes };
}

test('custom preview goes directly to fixed-pace controls and displays honest results', async ({ page }) => {
  const { errors, writes } = await prepare(page);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.locator('select')).toHaveCount(0);
  await expect(dialog.locator('#preview-chunkSize')).toHaveCount(0);
  await dialog.locator('#preview-speedWpm').fill('300');
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await page.clock.runFor(100);
  await expect(page.locator('.word-highlight-container .text-content')).toContainText('bir');
  await page.clock.runFor(250);
  await expect(page.locator('.chunk-group.highlight')).toHaveCount(1);
  await page.clock.runFor(1200);
  await expect(page.getByText('Metindeki kelime sayısı', { exact: true })).toBeVisible();
  await expect(page.getByText('Gösterim temposu (kelime/dk)', { exact: true })).toBeVisible();
  await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
  expect(overflow).toBe(false);
  expect(errors).toEqual([]);
  expect(writes).toEqual([]);
});

test('missing in-band content produces a clear error, not a fallback exercise', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Teacher', true);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.getByText('Bu egzersize uygun yayımlanmış metin yüklenemedi.', { exact: false })).toBeVisible();
  await expect(page.locator('.start-button-large')).toHaveCount(0);
  expect(errors).toEqual([]);
  expect(writes).toEqual([]);
});

test('student uses tracked reading start/finish and server-owned tempo without preview privileges', async ({ page }) => {
  const { errors } = await prepare(page, 'Student');
  await expect(page.getByRole('button', { name: 'Özel ayarlarla dene' })).toHaveCount(0);
  const sessionId = '62000000-0000-4000-8000-000000000004';
  const actions = [];
  let completions = 0;
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = {};
    if (path.endsWith('/start')) response = { sessionId, exerciseId, totalSteps: 6, status: 'Active', configuration,
      initialData: { engineType: 'word_highlight', exerciseTypeName: 'SpeedReading', content: text, totalSteps: 6,
        wordCount: 6, questions: [], groupingChunkSize: 1, groupingDisplayPaceWpm: 200, readingMinimumMs: 1800 } };
    else if (path.endsWith('/validate')) {
      actions.push(route.request().postDataJSON().action);
      response = { isValid: true, isCompleted: actions.includes('finish_reading'), feedbackData: {} };
    } else if (path.endsWith('/complete')) {
      completions++;
      const payload = route.request().postDataJSON();
      expect(payload.rawWPM).toBeUndefined();
      expect(payload.accuracy).toBeUndefined();
      response = { sessionId, timeSpentSeconds: 2, correctCount: 0, incorrectCount: 0, accuracy: null, score: null,
        rawWPM: null, xpGained: 0, measurementStatus: 'NotMeasured',
        detailedResults: { wordCount: 6, groupingDisplayPaceWpm: 200, groupingCompletionPercent: 100, readingIncomplete: false } };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.locator('.start-button-large').click();
  await expect.poll(() => completions).toBe(1);
  expect(actions).toEqual(['start_reading', 'finish_reading']);
  await expect(page.getByText('Metindeki kelime sayısı', { exact: true })).toBeVisible();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toHaveCount(0);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

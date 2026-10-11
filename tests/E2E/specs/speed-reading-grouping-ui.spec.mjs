import { test, expect } from '@playwright/test';

// Actual UI/engine, deterministic Identity/API fixtures; PostgreSQL persistence is tested separately.
const exerciseId = '30000000-0000-0000-0000-000000000001';
const typeId = '30000000-0000-0000-0000-000000000002';
const textId = '30000000-0000-0000-0000-000000000003';

async function prepare(page, role = 'Admin', timeout = false, realText = 'bir. iki üç dört.\n\nbeş altı.') {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Gruplama testi', difficultyLevel: 3,
    exerciseTypeName: 'Chunking', configurationJson: JSON.stringify({ engineType: 'word_highlight', readingTextId: textId,
      engineConfig: { mode: 'chunking', readingPurpose: 'practice', content: { chunkSize: 2, minWordCount: 5, maxWordCount: 8 }, pacer: { speedWpm: 200 },
        visuals: { highlightColor: 'blue' }, timing: timeout ? { timeLimitSec: 1 } : {} } }) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      const dailyProgress = role === 'Student' && path === '/api/speed-reading/daily-progress/complete-exercise';
      return route.fulfill({ status: dailyProgress ? 200 : 500,
        json: dailyProgress ? {} : { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Chunking', displayName: 'Gruplama', engineType: 'word_highlight', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, title: 'Metin', content: realText,
      difficultyLevel: 3, isActive: true, wordCount: 6, questions: [{ type: 1, correctAnswer: 'A', optionA: 'a', optionB: 'b', optionC: 'c', optionD: 'd' }] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Gruplama', exact: true })).toBeVisible();
  return { errors, writes };
}

async function launch(page) {
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await page.clock.runFor(100);
}

test('group colors, display pace and completion remain distinct from measured reading speed', async ({ page }) => {
  const { errors, writes } = await prepare(page);
  await launch(page);
  const active = page.locator('.chunk-group.highlight');
  await expect(active.locator('.word-item')).toHaveCount(1);
  await expect(page.locator('.chunk-group')).toHaveCount(4);
  await expect(page.locator('.line-break')).toHaveCount(1);
  await expect(active).toHaveCSS('background-color', 'rgb(219, 234, 254)');
  await expect(active).toHaveCSS('background-image', 'none');
  await expect(page.locator('.word-highlight-container .progress-info')).toContainText('Grup');
  await expect(page.locator('.word-highlight-container .game-stats')).toContainText('Gösterim temposu');
  await page.clock.runFor(2100);
  await expect(page.getByText('Tamamlanan gösterim', { exact: true })).toBeVisible();
  await expect(page.getByText('Metindeki grup sayısı', { exact: true })).toBeVisible();
  await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  await expect(page.locator('.comprehension-questions-container')).toHaveCount(0);
  expect(errors).toEqual([]);
  expect(writes).toEqual([]);
});

test('large custom groups wrap on mobile without horizontally clipping real words', async ({ page }) => {
  await prepare(page, 'Admin', false, 'Kütüphanedekilerin gözlemlerinden yararlanılabileceğini düşündüğümüzde karşılaştırmalarımızı değerlendiriyoruz.');
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByLabel('Kelime grubu', { exact: true }).fill('10');
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await page.clock.runFor(100);
  const display = page.locator('.highlight-display-area');
  const dimensions = await display.evaluate(element => ({ width: element.clientWidth, scrollWidth: element.scrollWidth }));
  expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.width + 1);
});

test('teacher timeout reports incomplete demonstration without preview persistence', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Teacher', true);
  await launch(page);
  await page.clock.runFor(1100);
  await expect(page.getByText('Gösterim yarım kaldı.', { exact: false })).toBeVisible();
  expect(errors).toEqual([]);
  expect(writes).toEqual([]);
});

test('student cannot use staff-only custom preview controls', async ({ page }) => {
  await prepare(page, 'Student');
  await expect(page.getByRole('button', { name: 'Özel ayarlarla dene' })).toHaveCount(0);
});

test('student persists server-acknowledged grouping without a fabricated WPM', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Student');
  const sessionId = '30000000-0000-0000-0000-000000000004';
  const actions = [];
  let completions = 0;
  let completionPayload;
  const configuration = { engineType: 'word_highlight', engineConfig: {
    mode: 'chunking', readingPurpose: 'practice', pacer: { chunkSize: 4, speedWpm: 900 } } };
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = {};
    if (path.endsWith('/start')) response = {
      sessionId, exerciseId, totalSteps: 4, status: 'Active', configuration,
      initialData: { engineType: 'word_highlight', exerciseTypeName: 'Chunking',
        content: 'bir. iki üç dört.\n\nbeş altı.', totalSteps: 4, groupingVersion: 1,
        groupingChunks: [{ endIndex: 1, hasNewline: false }, { endIndex: 3, hasNewline: false },
          { endIndex: 4, hasNewline: true }, { endIndex: 6, hasNewline: false }],
        groupingChunkSize: 2, groupingDisplayPaceWpm: 200, readingMinimumMs: 1800 }
    };
    else if (path.endsWith('/validate')) {
      actions.push(route.request().postDataJSON().action);
      response = { isValid: true, isCompleted: actions.includes('finish_reading'), feedbackData: {} };
    } else if (path.endsWith('/complete')) {
      completionPayload = route.request().postDataJSON();
      completions++;
      response = { sessionId, correctCount: 0, incorrectCount: 0, accuracy: null, score: 0,
        rawWPM: null, xpGained: 0, measurementStatus: 'Unmeasured',
        detailedResults: { wordCount: 6, totalSteps: 4, groupingDisplayPaceWpm: 200, groupingCompletionPercent: 100, readingIncomplete: false } };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.locator('.start-button-large').click();
  await expect(page.locator('.chunk-group')).toHaveCount(4);
  await expect(page.locator('.word-highlight-container .game-stats')).toContainText('200');
  await expect.poll(() => completions).toBe(1);
  expect(actions).toEqual(['start_reading', 'finish_reading']);
  await expect.poll(() => writes).toEqual(['/api/speed-reading/daily-progress/complete-exercise']);
  expect(completionPayload.rawWPM).toBeUndefined();
  expect(completionPayload.wpm).toBeUndefined();
  expect(completionPayload.accuracy).toBeUndefined();
  await expect(page.getByText('Tamamlanan gösterim', { exact: true })).toBeVisible();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toHaveCount(0);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

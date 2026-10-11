import { test, expect } from '@playwright/test';
const exerciseId = '75000000-0000-4000-8000-000000000001';
const typeId = '75000000-0000-4000-8000-000000000002';
for (const timedOut of [false, true]) {
  test(`Skimming ${timedOut ? 'deadline' : 'manual finish'} inspects text then verifies main idea`, async ({ page }) => {
    let completed = 0; const actions = [], errors = [];
    const configuration = { engineType: 'skimming', engineConfig: { timeLimitSeconds: 1,
      content: { text: 'Eski katalog metni' }, targets: { words: ['eski'] }, visuals: { fontSize: '24px' } } };
    const question = { questionId: '75000000-0000-4000-8000-000000000003', questionText: 'Metnin ana fikri nedir?',
      optionA: 'Okumanın yararları', optionB: 'Ulaşım', optionC: 'Spor', optionD: 'Hava', targetTimeSeconds: 20 };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'Skimming', title: 'Göz Gezdirme', difficultyLevel: 3, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Skimming', displayName: 'Göz Gezdirme', engineType: 'skimming', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-skimming', exerciseId, totalSteps: 2, status: 'Active', configuration,
        initialData: { totalSteps: 2, exerciseTypeName: 'Skimming', engineType: 'skimming', content: 'Kitaplar öğrenmeyi destekler.\nOkuma yeni düşünceler kazandırır.',
          readingTextTitle: 'Okumanın katkıları', wordCount: 8, readingPurpose: 'evaluation', questions: [question],
          skimmingProtocolVersion: 1, readingMinimumMs: 300, readingMaximumMs: timedOut ? 1200 : 10000 } };
      else if (path.endsWith('/validate')) {
        const action = request.postDataJSON(); actions.push(action);
        response = { isValid: true, isCorrect: action.action === 'answer_question', correctAnswer: action.action === 'answer_question' ? 'A' : null,
          isCompleted: action.action === 'answer_question', currentWPM: null };
      } else if (path.endsWith('/exercise-sessions/owned-skimming/complete')) {
        completed++; response = { score: 100, accuracy: 100, rawWPM: null, wordsRead: null, comprehensionScore: 100,
          weightedKDP: null, measurementStatus: 'Measured', xpGained: 0,
          detailedResults: { skimmingInspectionMs: timedOut ? 1200 : 600, readingIncomplete: timedOut } };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await expect(page.getByText(/Süre dolarsa sorulara geçilir; çalışma eksik sayılır/)).toBeVisible();
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toContainText('Kitaplar öğrenmeyi destekler.');
    await expect(page.locator('.reading-instruction')).toContainText('ana fikrini belirleyin');
    await expect(page.locator('.scan-targets-panel')).toHaveCount(0);
    await expect(page.locator('.reading-text-content')).toHaveCSS('font-size', '24px');
    if (!timedOut) {
      const finish = page.getByRole('button', { name: 'İncelemeyi bitir, Sorulara geç' });
      await expect(finish).toBeEnabled(); await finish.click();
    }
    await expect(page.getByText(question.questionText, { exact: true })).toBeVisible();
    await page.getByRole('button', { name: /Okumanın yararları/ }).click();
    const next = page.getByRole('button', { name: /Sonuçları Gör|Sonraki Soru/ });
    await next.click();
    await expect.poll(() => completed).toBe(1);
    await expect(page.getByText(/metne göz gezdirme süresi normal okuma hızı/)).toBeVisible();
    await expect(page.getByText(/Ana fikir başarısı: 100%/)).toBeVisible();
    await expect(page.locator('.measurement-not-available > div').filter({ hasText: 'İnceleme süresi:' })).toHaveCount(1);
    await expect(page.locator('.measurement-not-available > div').filter({ hasText: 'Ana fikir başarısı:' })).toHaveCount(1);
    expect(actions.filter(action => action.action === 'start_reading')).toHaveLength(1);
    expect(actions.filter(action => action.action === 'finish_reading')).toHaveLength(1);
    expect(actions.find(action => action.action === 'finish_reading').isTimeout).toBe(timedOut);
    expect(actions.filter(action => action.action === 'answer_question')).toHaveLength(1);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
    await page.screenshot({ path: `../../artifacts/skimming-ui/${test.info().project.name}-${timedOut ? 'timeout' : 'manual'}.png`, fullPage: true });
  });
}

// Real local UI/engine; Identity and API are fixtures. Persistence is checked separately in PostgreSQL.
async function preparePreview(page, role = 'Admin', insufficient = false) {
  const textId = '75000000-0000-4000-8000-000000000010';
  const configuration = { engineType: 'skimming', engineConfig: {
    rules: { timeLimit: 150 }, timing: { minReadingTimeMs: 1000 },
    content: { minWordCount: 20, maxWordCount: 30, questionCount: 4 }, visuals: { fontSize: '24px' }
  } };
  const exercise = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'Skimming',
    title: 'Göz Gezdirme', difficultyLevel: 3, configurationJson: JSON.stringify(configuration) };
  const text = { id: textId, exerciseId, difficultyLevel: 3, title: 'Okumanın katkıları', isActive: true,
    content: Array(8).fill('Kitaplar öğrenmeyi destekler.').join(' '), wordCount: 24,
    questions: Array.from({ length: insufficient ? 1 : 4 }, (_, index) => ({
      id: `75000000-0000-4000-8000-00000000002${index}`, questionText: `Ana fikir sorusu ${index + 1}`,
      type: 1, bloomLevel: 2, correctAnswer: 'A', optionA: 'Öğrenme', optionB: 'Ulaşım',
      optionC: 'Spor', optionD: 'Hava', explanation: 'Metnin konusu öğrenmedir.'
    })) };
  const writes = [], errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', roles: [role],
      token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role })).toString('base64url')}.fixture` };
    else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: { message: 'Unexpected preview write' } });
    } else if (path.endsWith('/exercise-types')) response = {
      items: [{ id: typeId, name: 'Skimming', displayName: 'Göz Gezdirme', engineType: 'skimming', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith('/reading-texts')) response = [text];
    else if (path.endsWith(`/reading-texts/${textId}`)) response = text;
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises');
  await expect(page.getByRole('heading', { name: 'Göz Gezdirme', exact: true })).toBeVisible();
  return { writes, errors };
}

for (const custom of [false, true]) {
  test(`Skimming ${custom ? 'teacher custom' : 'admin catalogue'} preview uses its deadline and four questions without writes`, async ({ page }) => {
    const { writes, errors } = await preparePreview(page, custom ? 'Teacher' : 'Admin');
    await page.clock.install();
    if (custom) {
      await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
      const dialog = page.getByRole('dialog');
      const duration = dialog.getByLabel('İnceleme süresi sınırı (saniye)');
      await expect(duration).toHaveValue('150');
      await duration.fill('1');
      await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
      await expect(dialog.getByRole('alert')).toBeVisible();
      await duration.fill('2');
      await dialog.getByLabel('Metin boyutu (px)').fill('28');
      await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    } else await page.getByRole('button', { name: 'Kayıtlı ayarlarla başlat' }).click();
    await expect(page.getByText(`İnceleme sınırı ${custom ? 2 : 150} saniyedir.`, { exact: false })).toBeVisible();
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toHaveCSS('font-size', custom ? '28px' : '24px');
    const finish = page.getByRole('button', { name: 'İncelemeyi bitir, Sorulara geç' });
    await expect(finish).toBeDisabled();
    if (custom) await page.clock.runFor(2100);
    else {
      const pause = page.locator('.reading-comprehension-container .game-header button').first();
      await pause.click();
      await page.clock.runFor(2000);
      await expect(finish).toBeDisabled();
      await pause.click();
      await page.clock.runFor(1100);
      await expect(finish).toBeEnabled();
      await finish.click();
    }
    for (let index = 1; index <= 4; index++) {
      await expect(page.locator('.question-text')).toHaveText(`Ana fikir sorusu ${index}`);
      await page.getByRole('button', { name: /Öğrenme/ }).click();
      await page.getByRole('button', { name: index === 4 ? 'Sonuçları Gör' : 'Sonraki Soru' }).click();
    }
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
    await expect(page.getByText(/Ana fikir başarısı: 100%/)).toBeVisible();
    expect(writes).toEqual([]);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  });
}

test('Skimming preview refuses a text with fewer than four valid questions', async ({ page }) => {
  const { writes, errors } = await preparePreview(page, 'Admin', true);
  await page.getByRole('button', { name: 'Kayıtlı ayarlarla başlat' }).click();
  await expect(page.getByText('Bu egzersize uygun yayımlanmış metin yüklenemedi. Metin ve gerekiyorsa sorularını kontrol edin.')).toBeVisible();
  await expect(page.locator('.start-button-large')).toHaveCount(0);
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

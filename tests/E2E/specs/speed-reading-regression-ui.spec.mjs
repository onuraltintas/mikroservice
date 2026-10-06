import { test, expect } from '@playwright/test';

// Actual UI/engine with deterministic API fixtures; real persistence has separate PostgreSQL tests.
const exerciseId = '40000000-0000-0000-0000-000000000001';
const typeId = '40000000-0000-0000-0000-000000000002';
const textId = '40000000-0000-0000-0000-000000000003';
const questionId = '40000000-0000-0000-0000-000000000004';
const question = { questionId, questionText: 'Metindeki ilk kelime hangisi?', optionA: 'bir', optionB: 'iki', optionC: 'üç', optionD: 'dört' };
const configuration = { engineType: 'regression_reduction', readingTextId: textId,
  engineConfig: { wpm: 600, chunkSize: 2, maskingType: 'trailing' } };

async function prepare(page, role = 'Admin', withQuestions = false) {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Regresyon testi', difficultyLevel: 3,
    exerciseTypeName: 'RegressionReduction', configurationJson: JSON.stringify(withQuestions
      ? { ...configuration, engineConfig: { ...configuration.engineConfig, readingPurpose: 'evaluation' } } : configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'RegressionReduction', displayName: 'Regresyon Azaltma', engineType: 'regression_reduction', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, content: 'bir iki üç dört beş', wordCount: 5, questions: withQuestions ? [question] : [] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Regresyon Azaltma', exact: true })).toBeVisible();
  return { errors, writes };
}

for (const role of ['Admin', 'Teacher']) test(`${role} custom preview displays effective pace, pauses and does not save results`, async ({ page }) => {
  const { errors, writes } = await prepare(page, role);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await expect(page.locator('.regression-footer')).toContainText('Gösterim temposu');
  await expect(page.locator('.regression-word.active')).toHaveCount(2);
  const pause = page.locator('.regression-reduction-container .game-header button').first();
  await pause.click(); await page.clock.runFor(1000);
  await expect(page.locator('.regression-word.active')).toHaveText(['bir', 'iki']);
  await pause.click(); await page.clock.runFor(501);
  await expect(page.getByRole('status').filter({ hasText: /Önizleme sonucu.*kaydedilmedi/ })).toBeVisible();
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('teacher question preview remains unmeasured and never writes answers', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Teacher', true);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.locator('.start-button-large').click();
  await expect(page.locator('.regression-question-container')).toBeVisible();
  await page.locator('.regression-question-container .option-button').first().click();
  await page.locator('.regression-question-container .submit-answer-btn').click();
  await expect(page.getByRole('status').filter({ hasText: /Önizleme sonucu.*kaydedilmedi/ })).toBeVisible();
  await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('student waits for validated answers and completes only once', async ({ page }) => {
  const { errors } = await prepare(page, 'Student');
  await expect(page.getByRole('button', { name: 'Özel ayarlarla dene' })).toHaveCount(0);
  const actions = []; let answers = 0, completions = 0;
  let releaseAnswer;
  await page.route('**/api/**/daily-progress/complete-exercise', route => route.fulfill({ json: {} }));
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = {};
    if (path.endsWith('/start')) response = { sessionId: 'owned-session', exerciseId, totalSteps: 6, status: 'Active', configuration,
      initialData: { engineType: 'regression_reduction', content: 'bir iki üç dört beş', questions: [question], totalSteps: 6 } };
    else if (path.endsWith('/validate')) {
      const action = route.request().postDataJSON(); actions.push(action.action);
      if (action.action === 'answer_question') {
        answers++; expect(action.questionId).toBe(questionId);
        await new Promise(resolve => releaseAnswer = resolve);
      }
      response = { isValid: true, isCorrect: action.action === 'answer_question', correctAnswer: 'A', isCompleted: action.action === 'answer_question' };
    } else if (path.endsWith('/complete')) {
      completions++; response = { correctCount: 1, incorrectCount: 0, accuracy: 100, score: 100, rawWPM: null,
        comprehensionScore: 100, measurementStatus: 'Measured', detailedResults: {} };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await page.locator('.start-button-large').click();
  await expect(page.locator('.regression-question-container')).toBeVisible();
  await page.locator('.regression-question-container .option-button').first().click();
  const submit = page.locator('.regression-question-container .submit-answer-btn');
  await submit.click(); await expect.poll(() => answers).toBe(1);
  await expect(submit).toBeDisabled(); expect(completions).toBe(0);
  releaseAnswer(); await expect.poll(() => completions).toBe(1);
  expect(actions).toEqual(['start_reading', 'finish_reading', 'answer_question']);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

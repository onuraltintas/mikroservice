import { test, expect } from '@playwright/test';

// Real Angular UI/engine, mock Identity and API; persistence is tested separately with PostgreSQL.
const exerciseId = '50000000-0000-0000-0000-000000000001';
const typeId = '50000000-0000-0000-0000-000000000002';
const textId = '50000000-0000-0000-0000-000000000003';
const questionId = '50000000-0000-0000-0000-000000000004';
const question = { questionId, questionText: 'İlk kelime hangisi?', optionA: 'bir', optionB: 'iki', optionC: 'uc', optionD: 'dort' };

async function prepare(page, role, mode = 'highlight', questions = false) {
  const errors = [], writes = [];
  const configuration = { engineType: 'subvocalization_reduction', readingTextId: textId,
    engineConfig: { targetWpm: 600, chunkSize: 2, displayMode: mode, readingPurpose: questions ? 'evaluation' : 'practice' } };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'İç Ses testi', difficultyLevel: 3,
    exerciseTypeName: 'SubvocalizationReduction', configurationJson: JSON.stringify(configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'SubvocalizationReduction', displayName: 'İç Ses Azaltma', engineType: 'subvocalization_reduction', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, content: 'bir iki uc dort bes', wordCount: 5, questions: questions ? [question] : [] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'İç Ses Azaltma', exact: true })).toBeVisible();
  return { errors, writes, configuration };
}

for (const [role, mode] of [['Admin', 'highlight'], ['Teacher', 'rsvp'], ['Teacher', 'chunk']]) {
  test(`${role} ${mode} custom preview applies mode and pace without saving`, async ({ page }) => {
    const { errors, writes } = await prepare(page, role, mode);
    await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    await expect(page.locator('.subvoc-footer')).toContainText('Gösterim temposu');
    if (mode === 'highlight') await expect(page.locator('.subvoc-word.active')).toHaveText(['bir', 'iki']);
    else {
      await expect(page.locator('.subvoc-display-area')).toHaveClass(new RegExp(`${mode}-mode`));
      await expect(page.locator('.subvoc-current-chunk')).toHaveText('bir iki');
    }
    const pause = page.locator('.subvoc-reduction-container .game-header button').first();
    await pause.click(); await page.clock.runFor(1000);
    if (mode === 'highlight') await expect(page.locator('.subvoc-word.active')).toHaveText(['bir', 'iki']);
    else await expect(page.locator('.subvoc-current-chunk')).toHaveText('bir iki');
    await pause.click(); await page.clock.runFor(501);
    await expect(page.getByRole('status').filter({ hasText: /Önizleme sonucu.*kaydedilmedi/ })).toBeVisible();
    await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
    expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}

test('teacher question preview is unmeasured and never writes answers', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Teacher', 'chunk', true);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.locator('.start-button-large').click();
  await page.locator('.subvoc-reduction-container').waitFor({ state: 'hidden' });
  await page.locator('.modern-options .modern-option-btn').first().click();
  await expect(page.getByText('Önizleme: cevap değerlendirilmedi.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Tamamla', exact: false }).click();
  await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('student waits, retries failed validation and submits reading completion once', async ({ page }) => {
  const { errors, configuration } = await prepare(page, 'Student', 'highlight', true);
  const actions = []; let answers = 0, completions = 0, release;
  await page.route('**/api/**/daily-progress/complete-exercise', route => route.fulfill({ json: {} }));
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = {};
    if (path.endsWith('/start')) response = { sessionId: 'owned-session', exerciseId, totalSteps: 6, status: 'Active', configuration,
      initialData: { engineType: 'subvocalization_reduction', content: 'bir iki uc dort bes', questions: [question], totalSteps: 6 } };
    else if (path.endsWith('/validate')) {
      const action = route.request().postDataJSON(); actions.push(action.action);
      if (action.action === 'answer_question') {
        answers++; expect(action.questionId).toBe(questionId);
        if (answers === 1) return route.fulfill({ json: { isValid: false, message: 'Yeniden deneyin' } });
        await new Promise(resolve => release = resolve);
      }
      response = { isValid: true, isCorrect: action.action === 'answer_question', correctAnswer: 'A' };
    } else if (path.endsWith('/complete')) {
      completions++; response = { correctCount: 1, incorrectCount: 0, accuracy: 100, score: 100, rawWPM: null,
        comprehensionScore: 100, measurementStatus: 'Measured', detailedResults: {} };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await page.locator('.start-button-large').click();
  const answer = page.locator('.modern-options .modern-option-btn').first();
  await answer.click(); await expect.poll(() => answers).toBe(1);
  await expect(answer).toBeEnabled();
  await answer.click(); await expect.poll(() => answers).toBe(2);
  await expect(answer).toBeDisabled(); expect(completions).toBe(0);
  release();
  await page.getByRole('button', { name: 'Tamamla', exact: false }).click();
  await expect.poll(() => completions).toBe(1);
  expect(actions).toEqual(['start_reading', 'finish_reading', 'answer_question', 'answer_question']);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

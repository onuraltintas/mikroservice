import { test, expect } from '@playwright/test';
const exerciseId = '71000000-0000-4000-8000-000000000001';
const typeId = '71000000-0000-4000-8000-000000000002';
const words = ['merak', 'özen', 'sabır', 'sevinç'].map((word, i) => ({
  id: `71000000-0000-4000-8000-00000000001${i}`, word, definition: 'Anlam ' + i, category: 'Genel', difficultyLevel: 2, questionType: 'word', box: 3
}));
for (const flow of ['learning', 'review', 'quiz', 'learn-then-quiz']) {
  const learnThenQuiz = flow === 'learn-then-quiz', mode = learnThenQuiz ? 'learning' : flow;
  test(`student ${flow} persists answers once and never displays WPM`, async ({ page }) => {
    const errors = [], actions = []; let completed = 0;
    const configuration = { engineType: 'vocabulary_builder', engineConfig: { mode, learnThenQuiz, quizType: 'word_to_definition', words } };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) {
        const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Student' };
        response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: ['Student'] };
      } else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'Vocabulary', title: 'Kelime Hazinesi', difficultyLevel: 2, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Vocabulary', displayName: 'Kelime Hazinesi', engineType: 'vocabulary_builder', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-vocabulary', exerciseId, totalSteps: 4, status: 'Active', configuration, initialData: { vocabularyMode: mode, vocabularyLearnThenQuiz: learnThenQuiz, vocabularyWords: words, totalSteps: 4 } };
      else if (path.endsWith('/validate')) {
        const action = request.postDataJSON(); actions.push(action);
        response = { isValid: true, isCorrect: true, feedbackData: { box: 4 }, isCompleted: actions.length === 4 };
      } else if (path.endsWith('/exercise-sessions/owned-vocabulary/complete')) {
        completed++; response = { correctCount: 4, incorrectCount: 0, accuracy: 100, score: 100, rawWPM: null, wordsRead: null, measurementStatus: 'Measured', detailedResults: {} };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await page.locator('.start-button-large').click();
    for (let i = 0; i < 4; i++) {
      if (mode === 'quiz') {
        await page.locator('.option-btn').filter({ hasText: 'Anlam ' + i }).click();
        await expect(page.locator('.feedback-banner')).toContainText('Doğru');
        await page.locator('.next-btn').click();
      } else {
        await expect(page.locator('.word-text')).toHaveText(words[i].word);
        await page.locator('.word-card').click();
        await page.getByRole('button', { name: /\bBiliyorum$/ }).click();
      }
    }
    if (learnThenQuiz) {
      expect(completed).toBe(0);
      for (let i = 0; i < 4; i++) {
        await page.locator('.option-btn').filter({ hasText: 'Anlam ' + i }).click();
        await expect(page.locator('.feedback-banner')).toContainText('Doğru');
        await page.locator('.next-btn').click();
      }
    }
    await expect.poll(() => completed).toBe(1);
    expect(actions).toHaveLength(learnThenQuiz ? 8 : 4); expect(actions.every(a => a.action === 'vocabulary_review')).toBe(true);
    await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
    expect(errors).toEqual([]);
  });
}

for (const [role, mode, available] of [['Admin', 'quiz', 8], ['Teacher', 'learning', 8], ['Admin', 'quiz', 3]]) {
  test(`${role} custom ${mode} preview with ${available} words is isolated and handles shortages`, async ({ page }) => {
    const pool = Array.from({ length: available }, (_, i) => ({ id: `72000000-0000-4000-8000-${String(i).padStart(12, '0')}`, word: `Kelime${i}`, definition: `Anlam${i}`, difficultyLevel: 2 }));
    const configuration = { engineType: 'vocabulary_builder', engineConfig: { mode: 'learning', vocabulary: { count: 4, difficultyLevel: 2 }, quizType: 'word_to_definition' } };
    const writes = [], errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(({ exerciseId, mode }) => {
      localStorage.setItem('currentUser', JSON.stringify({ id: 'preview-user' }));
      history.replaceState({ customPreview: { exerciseId, values: { mode, wordCount: 8, quizType: 'definition_to_word', timeLimitPerWord: 0 } } }, '', location.href);
    }, { exerciseId, mode });
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      if (request.method() !== 'GET' && !path.endsWith('/auth/refresh-token')) writes.push(path);
      let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) {
        const claims = { sub: 'preview-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
        response = { id: 'preview-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
      } else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'Vocabulary', title: 'Kelime Hazinesi', difficultyLevel: 2, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Vocabulary', displayName: 'Kelime Hazinesi', engineType: 'vocabulary_builder', isActive: true }] };
      else if (path.includes('/vocabulary')) response = { items: pool, totalCount: pool.length };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await expect(page.getByText('Özel ayarlı önizleme — sonuç ve ilerleme kaydedilmez.')).toBeVisible();
    if (available < 8) {
      await expect(page.locator('.error-state')).toContainText('8 benzersiz kelime bulunamadı');
      await expect(page.locator('.loading-state')).toHaveCount(0);
    } else {
      await page.locator('.start-button-large').click();
      if (mode === 'learning') {
        for (let i = 0; i < 8; i++) { await page.locator('.word-card').click(); await page.getByRole('button', { name: /\bBiliyorum$/ }).click(); }
      }
      for (let i = 0; i < 8; i++) {
        const question = await page.locator('.word-highlight').textContent();
        const expected = pool.find(word => word.definition === question.trim());
        expect(expected).toBeTruthy();
        await page.locator('.option-btn').filter({ hasText: expected.word }).click();
        await expect(page.locator('.feedback-banner')).toContainText('Doğru');
        await page.locator('.next-btn').click();
      }
      await expect(page.getByText('Önizleme sonucu — kaydedilmedi.')).toBeVisible();
      await page.screenshot({ path: `../../artifacts/vocabulary-ui/${role}-${mode}-${test.info().project.name}.png`, fullPage: true });
    }
    expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}

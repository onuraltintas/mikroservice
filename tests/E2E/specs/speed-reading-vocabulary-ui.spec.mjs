import { test, expect } from '@playwright/test';
const exerciseId = '71000000-0000-4000-8000-000000000001';
const typeId = '71000000-0000-4000-8000-000000000002';
const words = ['merak', 'özen', 'sabır', 'sevinç'].map((word, i) => ({
  id: `71000000-0000-4000-8000-00000000001${i}`, word, definition: 'Anlam ' + i, category: 'Genel', difficultyLevel: 2, questionType: 'word', box: 3
}));
for (const mode of ['learning', 'review', 'quiz']) {
  test(`student ${mode} persists answers once and never displays WPM`, async ({ page }) => {
    const errors = [], actions = []; let completed = 0;
    const configuration = { engineType: 'vocabulary_builder', engineConfig: { mode, quizType: 'word_to_definition', words } };
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
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-vocabulary', exerciseId, totalSteps: 4, status: 'Active', configuration, initialData: { vocabularyMode: mode, vocabularyWords: words, totalSteps: 4 } };
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
        await page.getByRole('button', { name: 'Biliyorum', exact: true }).click();
      }
    }
    await expect.poll(() => completed).toBe(1);
    expect(actions).toHaveLength(4); expect(actions.every(a => a.action === 'vocabulary_review')).toBe(true);
    await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
    expect(errors).toEqual([]);
  });
}

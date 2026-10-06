import { test, expect } from '@playwright/test';
const exerciseId = '74000000-0000-4000-8000-000000000001';
const typeId = '74000000-0000-4000-8000-000000000002';
for (const evaluation of [false, true]) {
  test(`RSVP ${evaluation ? 'evaluation' : 'practice'} separates presentation from reading speed`, async ({ page }) => {
    let completed = 0; const actions = [], errors = [];
    // Deliberately stale catalog timing: the owned snapshot must prevail.
    const configuration = { engineType: 'text_stream', engineConfig: { mode: 'rsvp', intervalMs: 50, timing: { intervalMs: 0 }, visuals: { showFixation: true } } };
    const question = { questionId: '74000000-0000-4000-8000-000000000003', questionText: 'Metinde hangi nesne var?',
      optionA: 'Kitap', optionB: 'Araba', optionC: 'Telefon', optionD: 'Kalem', targetTime: 20 };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'RSVP', title: 'Seri Görsel Sunum', difficultyLevel: 3, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'RSVP', displayName: 'Seri Görsel Sunum', engineType: 'text_stream', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-rsvp', exerciseId, totalSteps: 4, status: 'Active', configuration,
        initialData: { totalSteps: 4, exerciseTypeName: 'RSVP', words: ['Bugün', 'bir', 'kitap', 'okudum.'], wordCount: 4,
          readingPurpose: evaluation ? 'evaluation' : 'practice', questions: evaluation ? [question] : [],
          rsvpProtocolVersion: 1, rsvpDisplayDurationMs: 500, rsvpGapMs: 100, rsvpFixationMs: 0, readingMinimumMs: 2300 } };
      else if (path.endsWith('/validate')) {
        const action = request.postDataJSON(); actions.push(action);
        response = { isValid: true, isCorrect: action.action === 'answer_question', isCompleted: action.action === 'answer_question', currentWPM: null };
      }
      else if (path.endsWith('/exercise-sessions/owned-rsvp/complete')) {
        completed++; response = { score: evaluation ? 100 : null, accuracy: evaluation ? 100 : null, rawWPM: null, wordsRead: null,
          comprehensionScore: evaluation ? 100 : null, weightedKDP: null, measurementStatus: evaluation ? 'Measured' : 'NotMeasured', xpGained: 0,
          detailedResults: { rsvpDisplayPaceWpm: 104.35, rsvpCompletionPercent: 100, rsvpPresentedWords: 4, readingIncomplete: false } };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await page.locator('.start-button-large').click();
    await expect(page.locator('.stimulus-display')).toBeVisible();
    await expect(page.locator('.text-stream-container .game-stats')).toContainText('Gösterim temposu');
    if (evaluation) {
      await expect(page.getByText(question.questionText, { exact: true })).toBeVisible();
      await page.getByRole('button', { name: /Kitap/ }).click();
      const next = page.getByRole('button', { name: /Sonuçları Gör|Sonraki Soru/ });
      if (await next.count()) await next.click();
    }
    await expect.poll(() => completed).toBe(1);
    await expect(page.getByText(/gösterim temposudur; ölçülmüş okuma hızı değildir/)).toBeVisible();
    await expect(page.getByText('Tahmini gösterilen kelime', { exact: true })).toBeVisible();
    expect(actions.some(action => action.action === 'start_reading')).toBe(true);
    expect(actions.some(action => action.action === 'finish_reading')).toBe(true);
    expect(actions.filter(action => action.action === 'answer_question').length).toBe(evaluation ? 1 : 0);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
  });
}

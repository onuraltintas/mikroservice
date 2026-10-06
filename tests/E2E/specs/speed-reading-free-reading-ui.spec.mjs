import { test, expect } from '@playwright/test';
const exerciseId = '72000000-0000-4000-8000-000000000001';
const typeId = '72000000-0000-4000-8000-000000000002';
for (const failure of [false, true]) {
  test(`free reading completion ${failure ? 'reports save failure' : 'saves once and labels untested tempo'}`, async ({ page }) => {
    let completed = 0; const actions = [], errors = [];
    const content = Array(100).fill('kelime').join(' ');
    const configuration = { engineType: 'free_reading', engineConfig: { display: { fontSize: 'large', lineHeight: 2.2 }, timing: { minReadingTimeMs: 500 } } };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'FreeReading', title: 'Serbest Okuma', difficultyLevel: 2, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'FreeReading', displayName: 'Serbest Okuma', engineType: 'free_reading', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-free', exerciseId, totalSteps: 100, status: 'Active', configuration, initialData: { content, wordCount: 100, readingTextTitle: 'Kaynak metin', questions: [] } };
      else if (path.endsWith('/validate')) { actions.push(request.postDataJSON()); response = { isValid: true, isCorrect: true }; }
      else if (path.endsWith('/exercise-sessions/owned-free/complete')) {
        completed++;
        if (failure) { await route.fulfill({ status: 503, json: { message: 'Test storage unavailable' } }); return; }
        response = { score: 0, accuracy: 0, rawWPM: 200, wordsRead: 100, comprehensionScore: null, weightedKDP: null, measurementStatus: 'Measured', detailedResults: {} };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toHaveClass(/font-large/);
    await expect(page.locator('.reading-text-content')).toHaveCSS('line-height', /\d+px/);
    await page.getByRole('button', { name: 'Okumayı Bitir' }).click();
    await expect.poll(() => completed).toBe(1);
    expect(actions.filter(a => a.action === 'start_reading')).toHaveLength(1);
    expect(actions.filter(a => a.action === 'finish_reading')).toHaveLength(1);
    if (!failure) {
      await expect(page.getByText('Anlama ölçülmedi; tempo, okumayı bitirdiğiniz beyanına dayanır.')).toBeVisible();
      await expect(page.locator('.wpm-box')).toContainText('200');
    } else await expect(page.getByText(/kaydedilemedi/i).first()).toBeVisible();
    expect(errors).toEqual([]);
  });
}

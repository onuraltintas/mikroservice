import { test, expect } from '@playwright/test';
const exerciseId = '73000000-0000-4000-8000-000000000001';
const typeId = '73000000-0000-4000-8000-000000000002';
for (const failure of [false, true]) {
  test(`error analysis ${failure ? 'retries an outage without guessing' : 'uses word indices and saves verified metrics'}`, async ({ page }) => {
    let completed = 0; const actions = [], errors = []; let selectionAttempts = 0, pauses = 0, resumes = 0;
    const configuration = { engineType: 'error_analysis', engineConfig: { display: { fontSize: 'large' }, timing: { timeLimitSec: 180 } } };
    const state = { selected: [], found: [], falseAlarms: [], hintUsedCount: 0, timeElapsed: 300, score: 100, accuracy: 100 };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'ErrorAnalysis', title: 'Hata Analizi', difficultyLevel: 2, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'ErrorAnalysis', displayName: 'Hata Analizi', engineType: 'error_analysis', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-error', exerciseId, totalSteps: 1, status: 'Active', configuration,
        initialData: { totalSteps: 1, errorAnalysisWords: [{ index: 3, text: 'yanlız' }, { index: 8, text: 'bugün' }], timeLimitSeconds: 180 } };
      else if (path.endsWith('/pause')) { pauses++; response = {}; }
      else if (path.endsWith('/resume')) { resumes++; response = {}; }
      else if (path.endsWith('/validate')) {
        const action = request.postDataJSON(); actions.push(action);
        if (action.action === 'error_analysis_select') {
          selectionAttempts++;
          if (failure && selectionAttempts <= 2) { await route.fulfill({ status: 503, json: { message: 'Test outage' } }); return; }
          expect(action.index).toBe(3); state.selected = [3]; state.found = [3];
        }
        if (action.action === 'error_analysis_hint') { state.hintUsedCount++; state.hintIndex = 3; }
        response = { isValid: true, isCorrect: action.action === 'error_analysis_select', isCompleted: state.found.length === 1, feedbackData: state };
      }
      else if (path.endsWith('/exercise-sessions/owned-error/complete')) {
        completed++; response = { score: 100, accuracy: 100, rawWPM: null, wordsRead: null, comprehensionScore: null, weightedKDP: null,
          measurementStatus: 'Measured', detailedResults: { totalSteps: 1, errorAnalysisFound: [3], errorAnalysisFalseAlarms: [], errorAnalysisHints: state.hintUsedCount } };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`); await page.locator('.start-button-large').click();
    const target = page.getByRole('button', { name: 'yanlız', exact: true });
    await expect(target).toBeEnabled(); await expect(page.locator('.error-text-content')).toHaveClass(/font-large/);
    if (!failure) {
      await page.getByRole('button', { name: 'pause', exact: true }).click(); await expect(target).toBeDisabled();
      await expect.poll(() => pauses).toBe(1); await page.getByRole('button', { name: 'play_arrow', exact: true }).click();
      await expect.poll(() => resumes).toBe(1); await expect(target).toBeEnabled();
      await page.getByRole('button', { name: 'İpucu', exact: true }).click(); await expect(target).toHaveClass(/hint/);
    }
    await target.click();
    if (failure) { await expect(page.getByRole('button', { name: 'Seçimi tekrar gönder' })).toBeVisible(); expect(completed).toBe(0); await page.getByRole('button', { name: 'Seçimi tekrar gönder' }).click(); }
    await expect.poll(() => completed).toBe(1); await expect(page.getByText('Bulunan hata', { exact: true })).toBeVisible();
    await expect(page.getByText(/Hata bulma alıştırmasıdır/)).toBeVisible(); expect(errors).toEqual([]);
    expect(actions.filter(action => action.action === 'error_analysis_select').every(action => action.index === 3)).toBe(true);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1); expect(overflow).toBe(false);
  });
}

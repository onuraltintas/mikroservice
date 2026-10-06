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
    if (await next.count()) await next.click();
    await expect.poll(() => completed).toBe(1);
    await expect(page.getByText(/metne göz gezdirme süresi normal okuma hızı/)).toBeVisible();
    await expect(page.getByText(/Ana fikir başarısı: 100%/)).toBeVisible();
    expect(actions.filter(action => action.action === 'start_reading')).toHaveLength(1);
    expect(actions.filter(action => action.action === 'finish_reading')).toHaveLength(1);
    expect(actions.find(action => action.action === 'finish_reading').isTimeout).toBe(timedOut);
    expect(actions.filter(action => action.action === 'answer_question')).toHaveLength(1);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
    await page.screenshot({ path: `../../artifacts/skimming-ui/${test.info().project.name}-${timedOut ? 'timeout' : 'manual'}.png`, fullPage: true });
  });
}

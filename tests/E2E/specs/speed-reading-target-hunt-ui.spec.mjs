import { test, expect } from '@playwright/test';

// Decision 29.10.2026: "Hedef Avı" replaces N-back in programs. The ready screen explains the rule with a sample row,
// every tap is checked by the (mocked) server and marked at once, and the result names found, missed and wrong taps.
const exerciseId = '74000000-0000-4000-8000-000000000001';
const typeId = '74000000-0000-4000-8000-000000000002';
const items = ['d', 'b', 'p', 'd', 'b', 'p', 'd', 'p', 'b', 'd', 'p', 'd', 'p', 'd', 'p', 'd', 'p', 'd', 'p', 'd',
  'p', 'd', 'p', 'd', 'p', 'd', 'p', 'd', 'p', 'd'];
const targets = items.flatMap((item, index) => item === 'b' ? [index] : []);
const configuration = { engineType: 'target_hunt',
  engineConfig: { target: 'b', distractors: ['d', 'p'], rows: 3, columns: 10, targetCount: 3, timeLimit: 60 } };

test('the student learns the rule from a sample row, gets instant feedback and sees a clear result', async ({ page }, info) => {
  const errors = [], actions = []; let completed = 0;
  const state = { hits: [], falseTaps: [], timeElapsed: 0 };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'TargetHunt', title: 'Hedef Avı: "b" harfi (Seviye 1)', difficultyLevel: 1, configurationJson: JSON.stringify(configuration) };
    else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'TargetHunt', displayName: 'Hedef Avı', engineType: 'target_hunt', isActive: true }] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-hunt', exerciseId, totalSteps: 3, status: 'Active', configuration,
      initialData: { totalSteps: 3, targetHuntItems: items, targetHuntTarget: 'b', targetHuntColumns: 10, timeLimitSeconds: 60 } };
    else if (path.endsWith('/validate')) {
      const action = request.postDataJSON(); actions.push(action); let isCorrect = null;
      if (action.action === 'target_hunt_tap') {
        isCorrect = items[action.index] === 'b';
        (isCorrect ? state.hits : state.falseTaps).push(action.index);
      }
      state.timeElapsed += 1000;
      const done = state.hits.length === 3;
      response = { isValid: true, isCorrect, isCompleted: done,
        feedbackData: { ...state, score: done ? 95 : 0, accuracy: done ? 75 : 0 } };
    }
    else if (path.endsWith('/exercise-sessions/owned-hunt/complete')) {
      completed++;
      response = { score: 95, accuracy: 75, rawWPM: null, wordsRead: null, comprehensionScore: null, weightedKDP: null,
        measurementStatus: 'Measured', correctCount: 3, incorrectCount: 1,
        detailedResults: { totalSteps: 3, targetHuntTarget: 'b', targetHuntHits: state.hits, targetHuntFalseTaps: state.falseTaps } };
    }
    await route.fulfill({ json: response });
  });

  await page.goto(`/student/exercises/universal-player/${exerciseId}`);
  await expect(page.getByText(/gördüğünde ona dokun/)).toBeVisible();
  await expect(page.locator('.target-hunt-example__item.is-target').first()).toHaveText('b');
  await page.screenshot({ path: info.outputPath('ready.png'), fullPage: true });

  await page.locator('.start-button-large').click();
  const cells = page.locator('.target-hunt-cell');
  await expect(cells).toHaveCount(30);
  await expect.poll(() => actions.some(action => action.action === 'target_hunt_start')).toBe(true);

  await cells.nth(0).click();
  await expect(cells.nth(0)).toHaveClass(/is-wrong/);
  await expect(page.getByText('Bu b değil.')).toBeVisible();
  await expect(page.getByText('Yanlış dokunuş: 1')).toBeVisible();

  await cells.nth(targets[0]).click();
  await expect(cells.nth(targets[0])).toHaveClass(/is-hit/);
  await expect(page.getByText('Doğru!')).toBeVisible();
  await page.screenshot({ path: info.outputPath('running.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);

  await cells.nth(targets[1]).click();
  await expect(cells.nth(targets[1])).toHaveClass(/is-hit/);
  await cells.nth(targets[2]).click();
  await expect.poll(() => completed).toBe(1);
  await expect(page.getByText('Bulunan hedef', { exact: true })).toBeVisible();
  await expect(page.getByText(/Dikkat ve görsel ayırt etme alıştırmasıdır/)).toBeVisible();
  await page.screenshot({ path: info.outputPath('result.png'), fullPage: true });
  expect(actions.filter(action => action.action === 'target_hunt_tap').map(action => action.index)).toEqual([0, ...targets]);
  expect(errors).toEqual([]);
});

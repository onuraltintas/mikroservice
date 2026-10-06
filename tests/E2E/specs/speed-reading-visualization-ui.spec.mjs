import { test, expect } from '@playwright/test';

// Real UI and engine; API fixtures. PostgreSQL persistence has separate tests.
const exerciseId = '60000000-0000-0000-0000-000000000001';
const typeId = '60000000-0000-0000-0000-000000000002';
const sceneId = '60000000-0000-0000-0000-000000000003';
const questionId = '60000000-0000-0000-0000-000000000004';
async function prepare(page, role, mode) {
  const errors = [], writes = [];
  const scene = { sceneId, description: 'Kırmızı evin yanında mavi bir bisiklet var.', duration: 5, displayOrder: 0,
    ...(mode === 'guided' ? { steps: ['Kırmızı bir ev düşünün.', 'Yanında mavi bir bisiklet düşünün.'], stepDurationMs: 3000 } : {}),
    questions: [{ questionId, questionText: 'Ev ne renk?', options: ['Kırmızı', 'Mavi'], questionType: 'color' }] };
  const configuration = { engineType: 'visualization', engineConfig: { mode, scenes: [scene] } };
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Görselleştirme testi', difficultyLevel: 3,
    exerciseTypeName: 'Visualization', configurationJson: JSON.stringify(configuration) };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Visualization', displayName: 'Görselleştirme', engineType: 'visualization', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Görselleştirme', exact: true })).toBeVisible();
  return { errors, writes, configuration, scene };
}

for (const mode of ['static', 'guided', 'flash']) {
  test(`teacher ${mode} custom preview preserves timing and never records scores`, async ({ page }) => {
    const { errors, writes } = await prepare(page, 'Teacher', mode);
    await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.locator('input[type=number]').first().fill(mode === 'guided' ? '1000' : '2');
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    const pause = page.locator('.visualization-container button').first();
    await page.clock.runFor(400); await pause.click();
    await expect(page.getByRole('button', { name: 'Hazırım, Sorulara Geç' })).toBeDisabled();
    await page.clock.runFor(3000); await expect(page.locator('.visualization-container')).toBeVisible();
    await pause.click(); await page.clock.runFor(600);
    if (mode === 'guided') await expect(page.locator('.guided-step-text')).toContainText('mavi bir bisiklet');
    await page.clock.runFor(1000);
    await page.locator('.modern-options .modern-option-btn').first().click();
    await expect(page.getByText('Önizleme: cevap değerlendirilmedi.', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Sonraki Sahne' }).click();
    await expect(page.getByRole('status').filter({ hasText: /Önizleme sonucu.*kaydedilmedi/ })).toBeVisible();
    await expect(page.getByText('Anlama ölçülmedi.', { exact: false })).toBeVisible();
    expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}

test('student retries a network failure and completes one server-validated session', async ({ page }) => {
  const { errors, configuration, scene } = await prepare(page, 'Student', 'guided');
  let attempts = 0, completed = 0; const actions = [];
  await page.route('**/api/**/daily-progress/complete-exercise', route => route.fulfill({ json: {} }));
  await page.route('**/api/**/exercise-sessions/**', async route => {
    const path = new URL(route.request().url()).pathname; let response = {};
    if (path.endsWith('/start')) response = { sessionId: 'owned-visualization', exerciseId, totalSteps: 1, status: 'Active', configuration,
      initialData: { engineType: 'visualization', visualizationScenes: [scene], questions: [], totalSteps: 1 } };
    else if (path.endsWith('/validate')) {
      const action = route.request().postDataJSON(); actions.push(action.action);
      expect(action.action).toBe('answer_question'); expect(action.questionId).toBe(questionId); expect(action.answer).toBe('A');
      if (++attempts === 1) return route.fulfill({ status: 400, json: { message: 'Yeniden deneyin' } });
      response = { isValid: true, isCorrect: true, correctAnswer: 'A' };
    } else if (path.endsWith('/complete')) {
      completed++; response = { correctCount: 1, incorrectCount: 0, accuracy: 100, score: 100,
        rawWPM: null, comprehensionScore: 100, measurementStatus: 'Measured', detailedResults: {} };
    }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`, { waitUntil: 'domcontentloaded' });
  await page.locator('.start-button-large').click(); await page.getByRole('button', { name: 'Hazırım, Sorulara Geç' }).click();
  const answer = page.locator('.modern-options .modern-option-btn').first();
  await answer.click(); await expect.poll(() => attempts).toBe(1); await expect(answer).toBeVisible();
  await answer.click(); await expect(page.locator('.feedback-option.correct')).toContainText('Kırmızı');
  await page.getByRole('button', { name: 'Sonraki Sahne' }).click();
  await expect.poll(() => completed).toBe(1); expect(actions).toEqual(['answer_question', 'answer_question']);
  await expect(page.locator('.stat-label').filter({ hasText: /^WPM$|^Okuma hızı$/ })).toHaveCount(0);
  expect(errors).toEqual([]);
});

import { test, expect } from '@playwright/test';
test('exam shows authoritative passage and validates question before accepting answer', async ({ page }) => {
  const exerciseId = '77000000-0000-4000-8000-000000000001', typeId = '77000000-0000-4000-8000-000000000002';
  const questionId = '77000000-0000-4000-8000-000000000003';
  const configuration = { engineType: 'exam_simulation', engineConfig: { timing: { questionTimeSeconds: 30 } } };
  const actions = []; let completed = 0; const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname; let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'ExamSimulation', title: 'Sınav Simülasyonu', difficultyLevel: 3, configurationJson: JSON.stringify(configuration) };
    else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'ExamSimulation', displayName: 'Sınav Simülasyonu', engineType: 'exam_simulation', isActive: true }] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'exam-test', configuration, initialData: { engineType: 'exam_simulation', content: 'Okuma yeni düşünceler kazandırır.', examQuestionTimeSeconds: 30,
      questions: [{ questionId, questionText: 'Metnin ana fikri nedir?', optionA: 'Okumanın katkısı', optionB: 'Ulaşım', optionC: 'Spor', optionD: 'Hava' }] } };
    else if (path.endsWith('/validate')) { const action = route.request().postDataJSON().action; actions.push(action); response = { isValid: true, isCorrect: action === 'answer_question', correctAnswer: 'A' }; }
    else if (path.endsWith('/exam-test/complete')) { completed++; response = { measurementStatus: 'Measured', rawWPM: null, comprehensionScore: 100, accuracy: 100, score: 100, xpGained: 0, detailedResults: {} }; }
    await route.fulfill({ json: response });
  });
  await page.goto(`/student/exercises/universal-player/${exerciseId}`);
  await page.locator('.start-button-large').click();
  await expect(page.locator('.exam-paragraph-content')).toContainText('Okuma yeni düşünceler kazandırır.');
  await expect.poll(() => actions[0]).toBe('exam_question_start');
  await expect(page.locator('.wpm-badge.target')).toHaveCount(0);
  await page.getByRole('button', { name: /Okumanın katkısı/ }).click();
  await expect.poll(() => actions[1]).toBe('answer_question');
  await page.getByRole('button', { name: /Sonuçları|Tamamla|Bitir/ }).first().click();
  await expect.poll(() => completed).toBe(1);
  expect(errors).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
});

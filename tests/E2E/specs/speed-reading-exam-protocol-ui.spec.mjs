import { test, expect } from '@playwright/test';
const exerciseId = '77000000-0000-4000-8000-000000000001', typeId = '77000000-0000-4000-8000-000000000002';
const questionId = '77000000-0000-4000-8000-000000000003';
const question = { questionId, questionText: 'Metnin ana fikri nedir?', type: 1, bloomLevel: 2,
  optionA: 'Okumanın katkısı', optionB: 'Ulaşım', optionC: 'Spor', optionD: 'Hava' };

for (const deadline of [false, true]) {
  test(`exam ${deadline ? 'reading deadline' : 'manual reading'} defers feedback and saves validated results`, async ({ page }) => {
    const configuration = { engineType: 'exam_simulation', engineConfig: {
      timing: { questionTimeSeconds: 30, minReadingTimeMs: 300, maxReadingTimeMs: deadline ? 1200 : 10000 },
      display: { fontSize: 'large', lineHeight: 2.2 } } };
    const actions = []; let completed = 0; const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'ExamSimulation', title: 'Süreli Paragraf Testi', difficultyLevel: 5, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'ExamSimulation', displayName: 'Sınav Simülasyonu', engineType: 'exam_simulation', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'exam-test', configuration, initialData: {
        engineType: 'exam_simulation', content: 'Okuma yeni düşünceler kazandırır.', examProtocolVersion: 1,
        readingMinimumMs: 300, readingMaximumMs: deadline ? 1200 : 10000, examQuestionTimeSeconds: 30, questions: [question] } };
      else if (path.endsWith('/validate')) {
        const action = route.request().postDataJSON().action; actions.push(action);
        response = { isValid: true, isCorrect: null, correctAnswer: null,
          feedbackData: action === 'exam_question_start' ? { remainingSeconds: 30 } : { responseTimeSeconds: 2 } };
      } else if (path.endsWith('/exam-test/complete')) {
        completed++; response = { measurementStatus: 'Measured', rawWPM: null, comprehensionScore: 100,
          accuracy: 100, score: 100, correctCount: 1, timeSpentSeconds: 3, xpGained: 0,
          detailedResults: { questions: [{ ...question, correctAnswer: 'A', explanation: 'Metin okumanın katkısını anlatır.' }],
            answers: [{ questionId, answer: 'A', isCorrect: true, timeSpentSeconds: 2 }] } };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toContainText('Okuma yeni düşünceler kazandırır.');
    await expect(page.locator('.reading-comprehension-container button').filter({ hasText: 'pause' })).toHaveCount(0);
    if (!deadline) await page.getByRole('button', { name: 'Okudum, Sorulara Geç' }).click();
    await expect(page.locator('.exam-paragraph-content')).toContainText('Okuma yeni düşünceler kazandırır.');
    await expect(page.locator('.paragraph-text')).toHaveCSS('font-size', '24px');
    await expect(page.locator('.paragraph-text')).toHaveCSS('line-height', '52.8px');
    await expect.poll(() => actions.slice(0, 3)).toEqual(['start_reading', 'finish_reading', 'exam_question_start']);
    await page.getByRole('button', { name: /Okumanın katkısı/ }).click();
    await expect(page.getByText('Cevabınız kaydedildi. Doğru cevaplar değerlendirme sonunda gösterilecek.')).toBeVisible();
    await expect(page.locator('.answer-option.correct, .answer-option.wrong')).toHaveCount(0);
    await page.getByRole('button', { name: 'Sonuçları Gör' }).click();
    await expect.poll(() => completed).toBe(1);
    await expect(page.locator('.exam-results .questions-box .result-value')).toHaveText('1/1');
    await expect(page.locator('.exam-results .time-box .result-value')).toHaveText('0:03');
    await expect(page.locator('.question-answers-detail .status-icon')).toHaveCSS('font-family', /Material Icons/);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
    await page.getByRole('button', { name: 'Kapat', exact: true }).click();
    await page.screenshot({ path: `../../artifacts/exam-ui/${test.info().project.name}-${deadline ? 'deadline' : 'manual'}.png`, fullPage: true });
  });
}

for (const role of ['Admin', 'Teacher', 'InsufficientContent']) {
  test(`${role} preview uses real content, configurable presentation and no writes`, async ({ page }) => {
    const textId = '77000000-0000-4000-8000-000000000010';
    const configuration = { engineType: 'exam_simulation', engineConfig: {
      content: { minWordCount: 4, maxWordCount: 20, questionCount: 1 },
      timing: { minReadingTimeMs: 200, maxReadingTimeMs: 10000, questionTimeSeconds: 30 },
      display: { fontSize: 'large', lineHeight: 2 } } };
    const writes = [];
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', roles: [role === 'InsufficientContent' ? 'Admin' : role],
        token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: role === 'InsufficientContent' ? 'Admin' : role })).toString('base64url')}.fixture` };
      else if (request.method() !== 'GET') { writes.push(path); return route.fulfill({ status: 500, json: {} }); }
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'ExamSimulation', displayName: 'Sınav Simülasyonu', engineType: 'exam_simulation', isActive: true }] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'ExamSimulation', title: 'Süreli Paragraf Testi', difficultyLevel: 5, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercises')) response = { items: [{ id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'ExamSimulation', title: 'Süreli Paragraf Testi', difficultyLevel: 5, configurationJson: JSON.stringify(configuration) }], totalCount: 1 };
      else if (path.endsWith('/reading-texts')) response = [{ id: textId, difficultyLevel: 5, wordCount: 4, isActive: true }];
      else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, difficultyLevel: 5, content: 'Okuma yeni düşünceler kazandırır.', wordCount: 4, isActive: true,
        questions: role === 'InsufficientContent' ? [] : [{ ...question, correctAnswer: 'A', explanation: 'Okumanın katkısı.' }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      await route.fulfill({ json: response });
    });
    if (role === 'Teacher') {
      await page.goto('/student/exercises');
      await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
      const dialog = page.getByRole('dialog');
      await dialog.getByLabel('Metin boyutu', { exact: true }).selectOption('large');
      await dialog.getByLabel('Satır aralığı (%)').fill('220');
      await dialog.getByLabel('İlk okuma süresi sınırı (saniye)').fill('20');
      await dialog.getByLabel('Soru başına süre (saniye)').fill('40');
      await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
      await expect(page.getByText('Özel ayarlı önizleme — sonuç ve ilerleme kaydedilmez.')).toBeVisible();
    } else await page.goto(`/student/exercises/universal-player/${exerciseId}?previewMode=true`);
    if (role === 'InsufficientContent') {
      await expect(page.getByText('Bu egzersize uygun yayımlanmış metin yüklenemedi. Metin ve gerekiyorsa sorularını kontrol edin.')).toBeVisible();
      await expect(page.locator('.start-button-large')).toHaveCount(0);
      expect(writes).toEqual([]);
      return;
    }
    await page.locator('.start-button-large').click();
    await page.getByRole('button', { name: 'Okudum, Sorulara Geç' }).click();
    await expect(page.locator('.paragraph-text')).toHaveCSS('font-size', '24px');
    await expect(page.locator('.paragraph-text')).toHaveCSS('line-height', role === 'Teacher' ? '52.8px' : '48px');
    await page.getByRole('button', { name: /Okumanın katkısı/ }).click();
    await expect(page.locator('.answer-option.correct')).toHaveCount(0);
    await page.getByRole('button', { name: 'Sonuçları Gör' }).click();
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.')).toBeVisible();
    expect(writes).toEqual([]);
  });
}

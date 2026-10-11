import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
const exerciseId = '74000000-0000-4000-8000-000000000001';
const typeId = '74000000-0000-4000-8000-000000000002';
for (const version of [1, 2]) for (const evaluation of [false, true]) {
  test(`RSVP v${version} ${evaluation ? 'evaluation' : 'practice'} separates presentation from reading speed`, async ({ page }) => {
    let completed = 0; const actions = [], errors = [];
    // Deliberately stale catalog timing: the owned snapshot must prevail.
    const configuration = { engineType: 'text_stream', engineConfig: { mode: 'rsvp', intervalMs: 50, timing: { intervalMs: 0 }, visuals: { showFixation: true } } };
    const question = { questionId: '74000000-0000-4000-8000-000000000003', questionText: 'Metinde hangi nesne var?',
      optionA: 'Kitap', optionB: 'Araba', optionC: 'Telefon', optionD: 'Kalem', targetTime: 20 };
    const questions = evaluation ? Array.from({ length: version === 2 ? 5 : 1 }, (_, index) => ({
      ...question, questionId: `74000000-0000-4000-8000-00000000000${index + 3}`, questionText: `${question.questionText} (${index + 1})`
    })) : [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
      localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' }));
      window.rsvpFixations = 0; let visible = false;
      document.addEventListener('DOMContentLoaded', () => new MutationObserver(() => {
        const current = Boolean(document.querySelector('.text-stream-container .fixation-cross'));
        if (current && !visible) window.rsvpFixations++;
        visible = current;
      }).observe(document.body, { childList: true, subtree: true }));
    });
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'RSVP', title: 'Seri Görsel Sunum', difficultyLevel: 3, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'RSVP', displayName: 'Seri Görsel Sunum', engineType: 'text_stream', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-rsvp', exerciseId, totalSteps: 4, status: 'Active', configuration,
        initialData: { totalSteps: 4, exerciseTypeName: 'RSVP', words: ['Bugün', 'bir', 'kitap', 'okudum.'], wordCount: 4,
          readingPurpose: evaluation ? 'evaluation' : 'practice', questions,
          rsvpProtocolVersion: version, rsvpDisplayDurationMs: 500, rsvpGapMs: 100,
          rsvpFixationMs: version === 2 ? 300 : 0, readingMinimumMs: version === 2 ? 2600 : 2300 } };
      else if (path.endsWith('/validate')) {
        const action = request.postDataJSON(); actions.push(action);
        response = { isValid: true, isCorrect: action.action === 'answer_question',
          isCompleted: action.action === 'answer_question' && actions.filter(item => item.action === 'answer_question').length === questions.length, currentWPM: null };
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
    for (const item of questions) {
      await expect(page.getByText(item.questionText, { exact: true })).toBeVisible();
      await page.getByRole('button', { name: /Kitap/ }).click();
      const next = page.getByRole('button', { name: /Sonuçları Gör|Sonraki Soru/ });
      await expect(next).toBeEnabled();
      await next.click();
    }
    await expect.poll(() => completed).toBe(1);
    await expect(page.getByText(/gösterim temposudur; ölçülmüş okuma hızı değildir/)).toBeVisible();
    await expect(page.getByText('Tahmini gösterilen kelime', { exact: true })).toBeVisible();
    expect(actions.some(action => action.action === 'start_reading')).toBe(true);
    expect(actions.some(action => action.action === 'finish_reading')).toBe(true);
    expect(actions.filter(action => action.action === 'answer_question').length).toBe(questions.length);
    expect(await page.evaluate(() => window.rsvpFixations)).toBe(version === 2 ? 1 : 0);
    expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
  });
}

for (const [role, available] of [['Admin', true], ['Teacher', true], ['Admin', false]]) {
  test(`${role} custom RSVP preview ${available ? 'uses prepared text and five questions without writes' : 'rejects insufficient questions'}`, async ({ page }) => {
    const settings = JSON.parse(readFileSync(new URL('../../../content-packs/rsvp/v2/settings.json', import.meta.url)))[0];
    const row = JSON.parse(readFileSync(new URL('../../../content-packs/rsvp/v2/content.json', import.meta.url)))
      .find(item => item.exerciseId === settings.id);
    const writes = [], errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(({ id }) => {
      localStorage.setItem('currentUser', JSON.stringify({ id: 'preview-user' }));
      history.replaceState({ customPreview: { exerciseId: id, values: { displayDurationMs: 50, intervalMs: 0 } } }, '', location.href);
    }, { id: settings.id });
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      if (request.method() !== 'GET' && !path.endsWith('/auth/refresh-token')) writes.push(path);
      let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'preview-user', roles: [role],
        token: `e30.${Buffer.from(JSON.stringify({ sub: 'preview-user', exp: Math.floor(Date.now()/1000)+3600, role })).toString('base64url')}.fixture` };
      else if (path.endsWith(`/exercises/${settings.id}`)) response = { id: settings.id, exerciseTypeId: typeId,
        exerciseTypeName: 'RSVP', title: 'Seri Görsel Sunum', difficultyLevel: row.level, targetAgeGroupConfigurationId: row.ageGroupId,
        configurationJson: JSON.stringify(settings.configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'RSVP', engineType: 'text_stream', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/reading-texts')) response = [{ ...row, difficultyLevel: row.level, isActive: true, targetAgeGroupConfigurationId: row.ageGroupId }];
      else if (path.endsWith(`/reading-texts/${row.id}`)) response = { ...row, difficultyLevel: row.level,
        targetAgeGroupConfigurationId: row.ageGroupId, questions: available ? row.questions : row.questions.slice(0, 4) };
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${settings.id}?mode=preview`);
    if (available) {
      await expect(page.getByText('Özel ayarlı önizleme — sonuç ve ilerleme kaydedilmez.')).toBeVisible();
      await page.locator('.start-button-large').click();
      const presentationPace = Math.round(row.wordCount * 60000 / (300 + row.wordCount * 50));
      await expect(page.locator('.text-stream-container .game-stats')).toContainText(String(presentationPace));
      for (const question of row.questions) {
        await expect(page.getByText(question.questionText, { exact: true })).toBeVisible({ timeout: 20000 });
        await page.getByRole('button', { name: `${question.correctAnswer} ${question['option' + question.correctAnswer]}`, exact: true }).click();
        const next = page.getByRole('button', { name: /Sonuçları Gör|Sonraki Soru/ });
        await expect(next).toBeEnabled(); await next.click();
      }
      await expect(page.getByText('Önizleme sonucu — kaydedilmedi.')).toBeVisible();
      await expect(page.getByText(/gösterim temposudur; ölçülmüş okuma hızı değildir/)).toBeVisible();
      await page.screenshot({ path: `../../artifacts/rsvp-ui/${role}-${test.info().project.name}.png`, fullPage: true });
    } else {
      await expect(page.locator('.error-state')).toContainText('uygun yayımlanmış metin yüklenemedi');
      await expect(page.locator('.loading-state')).toHaveCount(0);
    }
    expect(writes).toEqual([]); expect(errors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(false);
  });
}

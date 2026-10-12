import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

// Public exercise DTO fields captured from the authenticated live API on 2026-10-12.
// No user/session identifiers or authentication material are retained.
const legacyCatalog = JSON.parse(readFileSync(new URL('../fixtures/assessment-tachistoscope-live-api.json', import.meta.url), 'utf8'))
  .map(item => ({ ...item, configuration: JSON.parse(item.configurationJson) }));
const settings = JSON.parse(readFileSync(new URL('../../../content-packs/assessment/v2/settings.json', import.meta.url), 'utf8'));
const catalog = [...legacyCatalog, ...legacyCatalog.map(item => {
  const configuration = settings.find(row => row.id === item.id).configuration;
  return { ...item, title: `${item.title} v2`, configurationJson: JSON.stringify(configuration), configuration };
})];
const attemptId = '94000000-0000-0000-0000-000000000001';
const sessionId = '94000000-0000-0000-0000-000000000002';

for (const exercise of catalog) {
  for (const resumed of [false, true]) {
    test(`${exercise.title}: ${resumed ? 'resume post-training' : 'initial assessment'} stimulus and completion`, async ({ page }) => {
      const errors = [], writes = [];
      const protocol = exercise.configuration.engineConfig.assessmentProtocol;
      const startRound = resumed ? (protocol ? 5 : 2) : 0;
      let round = startRound;
      const { durationMs } = exercise.configuration.engineConfig.timing;
      const durationAt = index => protocol ? protocol.blockValues[Math.max(0, Math.floor((index - 3) / protocol.roundsPerBlock))] : durationMs;
      const count = exercise.configuration.engineConfig.content.count;
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'assessment-ui-test' })));
      await page.route('**/api/**', async route => {
        const request = route.request(), path = new URL(request.url()).pathname;
        let response = { items: [], totalCount: 0 };
        if (request.method() === 'POST') writes.push({ path, body: request.postDataJSON() });
        if (path.endsWith('/auth/refresh-token')) response = { id: 'assessment-ui-test', roles: ['Student'],
          token: `e30.${Buffer.from(JSON.stringify({ sub: 'assessment-ui-test', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture` };
        else if (path.endsWith(`/exercises/${exercise.id}`)) {
          const { configuration, ...dto } = exercise;
          response = dto;
        }
        else if (path.endsWith('/exercise-sessions/start')) response = { sessionId, status: resumed ? 2 : 1,
          configuration: exercise.configuration,
          initialData: { exerciseTypeName: 'Tachistoscope', engineType: 'text_stream',
            isAssessmentMode: true, textStreamMode: 'tachistoscope', content: '', assessmentProtocol: protocol,
            tachistoscope: { count, round, contentType: 'number', source: 'default_pool',
              displayDurationMs: durationAt(round), initialDurationMs: durationMs, targetLength: 3, assessmentProtocol: protocol,
              adaptiveEnabled: false, correctCount: null, incorrectCount: null, trials: [] } } };
        else if (path.endsWith('/resume')) response = { message: 'Resumed' };
        else if (path.endsWith('/validate')) {
          const action = request.postDataJSON();
          if (action.action === 'tachistoscope_present') response = { isValid: true,
            feedbackData: { round, stimulus: String(246 + round), targetLength: 3, displayDurationMs: durationAt(round), isPractice: round < 3 && !!protocol } };
          else if (action.action === 'tachistoscope_answer') response = { isValid: true, isCorrect: null,
            isCompleted: ++round === count, feedbackData: { round, targetLength: 3, displayDurationMs: durationAt(Math.min(round, count - 1)) } };
          else throw new Error(`Unexpected assessment action: ${action.action}`);
        } else if (path.endsWith('/complete')) response = { sessionId, score: 100, accuracy: 100, measurementStatus: 1,
          correctCount: count - (protocol ? 3 : 0), incorrectCount: 0, totalSteps: count, elapsedSeconds: 15, xpEarned: 0, levelUp: false,
          detailedResults: { assessmentBlockResults: protocol?.blockValues.map((value, index) => ({ block: index + 1, value, rounds: protocol.roundsPerBlock, correct: protocol.roundsPerBlock, accuracy: 100 })),
            tachistoscope: { trials: [], initialDurationMs: durationMs, displayDurationMs: durationMs } } };
        await route.fulfill({ json: response });
      });
      await page.goto(`/student/exercises/universal-player/${exercise.id}?assessmentMode=true&assessmentAttemptId=${attemptId}&assessmentPhase=${resumed ? 2 : 1}`);
      await expect(page.locator('.start-button-large')).toBeVisible();
      expect(writes.find(item => item.path.endsWith('/start')).body.assessmentAttemptId).toBe(attemptId);
      await page.clock.install(); await page.clock.pauseAt(new Date());
      await page.locator('.start-button-large').click();
      await expect(page.locator('.text-stream-container .word-count-display')).toBeVisible();
      for (let index = startRound; index < count; index++) {
        await page.clock.runFor(300);
        await expect(page.locator('.stimulus-display')).toHaveText(String(246 + index));
        await page.clock.runFor(durationAt(index));
        await page.locator('.answer-input').fill(String(246 + index));
        await page.locator('.submit-btn').click();
        await expect.poll(() => writes.filter(item => item.body.action === 'tachistoscope_answer').length)
          .toBe(index - startRound + 1);
        if (!protocol) await expect(page.locator('.text-stream-container .word-count-display')).toContainText(`${index + 1}/${count}`);
        await page.clock.runFor(Math.max(600, exercise.configuration.engineConfig.timing.intervalMs));
      }
      await expect(page.locator('.grid-save-status')).toContainText('Sonuç kaydedildi.');
      if (protocol) await expect(page.getByRole('region', { name: 'Blok sonuçları' })).toBeVisible();
      expect(writes.filter(item => item.path.endsWith('/complete'))).toHaveLength(1);
      expect(writes.some(item => /\/(reading|daily-progress)(\/|$)/.test(item.path))).toBe(false);
      expect(writes.filter(item => item.path.endsWith('/resume'))).toHaveLength(resumed ? 1 : 0);
      expect(errors).toEqual([]);
    });
  }
}

for (const row of settings.filter(item => item.type === 'Comprehension')) {
  test(`assessment ${row.ageGroupId}: text review without immediate answer feedback`, async ({ page }) => {
    const count = row.configuration.engineConfig.assessmentQuestionCount;
    const questions = Array.from({ length: count }, (_, index) => ({
      questionId: `95000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      questionText: `Soru ${index + 1}`, optionA: 'A seçeneği', optionB: 'B seçeneği', optionC: 'C seçeneği', optionD: 'D seçeneği'
    }));
    const content = 'Yaşa uygun tek okuma metni. ' .repeat(12);
    const errors = [], writes = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'assessment-ui-test' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      let response = { items: [], totalCount: 0 };
      if (request.method() === 'POST') writes.push(request.postDataJSON());
      if (path.endsWith('/auth/refresh-token')) response = { id: 'assessment-ui-test', roles: ['Student'],
        token: `e30.${Buffer.from(JSON.stringify({ sub: 'assessment-ui-test', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture` };
      else if (path.endsWith(`/exercises/${row.id}`)) response = { id: row.id, title: 'Anlama ölçümü', difficultyLevel: 1,
        configurationJson: JSON.stringify(row.configuration) };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId, status: 1, configuration: row.configuration,
        initialData: { exerciseTypeName: 'Comprehension', engineType: 'reading_comprehension', isAssessmentMode: true,
          assessmentTextReviewAllowed: true, comprehensionMode: 'evaluation', content, questions,
          readingTextContent: content, wordCount: 60, readingComprehensionProtocolVersion: 1 } };
      else if (path.endsWith('/validate')) response = { isValid: true, isCorrect: null, correctAnswer: null,
        feedbackData: { rawWPM: 120, readingElapsedMilliseconds: 30000 } };
      else if (path.endsWith('/complete')) response = { sessionId, score: 100, accuracy: 100, rawWPM: 120,
        comprehensionScore: 100, totalSteps: count, measurementStatus: 1, elapsedSeconds: 90, detailedResults: {} };
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${row.id}?assessmentMode=true&assessmentAttemptId=${attemptId}&assessmentPhase=1`);
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    await page.clock.runFor(30000);
    await page.locator('.complete-reading-btn').click();
    await expect(page.getByRole('button', { name: 'Metne tekrar bak' })).toBeVisible();
    await page.getByRole('button', { name: 'Metne tekrar bak' }).click();
    await page.clock.runFor(20);
    await expect(page.locator('#comprehension-review-text')).toContainText('Yaşa uygun tek okuma metni');
    await page.getByRole('button', { name: 'Metni gizle' }).click();
    await page.clock.runFor(20);
    await expect(page.locator('.countdown-timer')).toHaveCount(0);
    await page.locator('.answer-option').first().click();
    await page.clock.runFor(20);
    await expect(page.locator('.question-feedback')).toHaveCount(0);
    await expect(page.locator('.answer-option.correct')).toHaveCount(0);
    expect(errors).toEqual([]);
    expect(writes.filter(item => item.action === 'start_reading')).toHaveLength(1);
    expect(writes.filter(item => item.action === 'finish_reading')).toHaveLength(1);
  });
}

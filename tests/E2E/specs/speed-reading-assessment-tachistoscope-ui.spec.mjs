import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

// Public exercise DTO fields captured from the authenticated live API on 2026-10-12.
// No user/session identifiers or authentication material are retained.
const catalog = JSON.parse(readFileSync(new URL('../fixtures/assessment-tachistoscope-live-api.json', import.meta.url), 'utf8'))
  .map(item => ({ ...item, configuration: JSON.parse(item.configurationJson) }));
const attemptId = '94000000-0000-0000-0000-000000000001';
const sessionId = '94000000-0000-0000-0000-000000000002';

for (const exercise of catalog) {
  for (const resumed of [false, true]) {
    test(`${exercise.title}: ${resumed ? 'resume post-training' : 'initial assessment'} stimulus and completion`, async ({ page }) => {
      const errors = [], writes = [];
      let round = resumed ? 2 : 0;
      const { durationMs } = exercise.configuration.engineConfig.timing;
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
            isAssessmentMode: true, textStreamMode: 'tachistoscope', content: '',
            tachistoscope: { count, round, contentType: 'number', source: 'default_pool',
              displayDurationMs: durationMs, initialDurationMs: durationMs, targetLength: 3,
              adaptiveEnabled: false, correctCount: null, incorrectCount: null, trials: [] } } };
        else if (path.endsWith('/resume')) response = { message: 'Resumed' };
        else if (path.endsWith('/validate')) {
          const action = request.postDataJSON();
          if (action.action === 'tachistoscope_present') response = { isValid: true,
            feedbackData: { round, stimulus: String(246 + round), targetLength: 3, displayDurationMs: durationMs } };
          else if (action.action === 'tachistoscope_answer') response = { isValid: true, isCorrect: null,
            isCompleted: ++round === count, feedbackData: { round, targetLength: 3, displayDurationMs: durationMs } };
          else throw new Error(`Unexpected assessment action: ${action.action}`);
        } else if (path.endsWith('/complete')) response = { sessionId, score: 100, accuracy: 100, measurementStatus: 1,
          correctCount: count, incorrectCount: 0, totalSteps: count, elapsedSeconds: 15, xpEarned: 0, levelUp: false,
          detailedResults: { tachistoscope: { trials: [], initialDurationMs: durationMs, displayDurationMs: durationMs } } };
        await route.fulfill({ json: response });
      });
      await page.goto(`/student/exercises/universal-player/${exercise.id}?assessmentMode=true&assessmentAttemptId=${attemptId}&assessmentPhase=${resumed ? 2 : 1}`);
      await expect(page.locator('.start-button-large')).toBeVisible();
      expect(writes.find(item => item.path.endsWith('/start')).body.assessmentAttemptId).toBe(attemptId);
      await page.clock.install(); await page.clock.pauseAt(new Date());
      await page.locator('.start-button-large').click();
      await expect(page.locator('.text-stream-container .word-count-display')).toBeVisible();
      for (let index = resumed ? 2 : 0; index < count; index++) {
        await page.clock.runFor(300);
        await expect(page.locator('.stimulus-display')).toHaveText(String(246 + index));
        await page.clock.runFor(durationMs);
        await page.locator('.answer-input').fill(String(246 + index));
        await page.locator('.submit-btn').click();
        await expect.poll(() => writes.filter(item => item.body.action === 'tachistoscope_answer').length)
          .toBe(index - (resumed ? 2 : 0) + 1);
        await expect(page.locator('.text-stream-container .word-count-display')).toContainText(`${index + 1}/${count}`);
        await page.clock.runFor(Math.max(600, exercise.configuration.engineConfig.timing.intervalMs));
      }
      await expect(page.locator('.grid-save-status')).toContainText('Sonuç kaydedildi.');
      expect(writes.filter(item => item.path.endsWith('/complete'))).toHaveLength(1);
      expect(writes.some(item => /\/(reading|daily-progress)(\/|$)/.test(item.path))).toBe(false);
      expect(writes.filter(item => item.path.endsWith('/resume'))).toHaveLength(resumed ? 1 : 0);
      expect(errors).toEqual([]);
    });
  }
}

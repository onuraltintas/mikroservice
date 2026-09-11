import { test, expect } from '@playwright/test';
import { completeApiMfa } from '../support/totp.mjs';

const email = process.env.E2E_SPEED_READING_EMAIL ?? process.env.E2E_ADMIN_EMAIL;
const password = process.env.E2E_SPEED_READING_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD;
const focusExerciseId = process.env.E2E_SPEED_READING_FOCUS_EXERCISE_ID;
const runsSpeedReadingWrite = process.env.E2E_RUN_SPEED_READING_WRITE === 'true';
const isDisposable = process.env.E2E_DISPOSABLE_ENV === 'true';

async function authenticatedHeaders(request) {
  const loginResponse = await request.post('/api/auth/login', { data: { email, password } });
  expect(loginResponse.status()).toBe(200);
  const login = await completeApiMfa(request, await loginResponse.json());
  return { Authorization: `Bearer ${login.accessToken}` };
}

test.describe('Speed Reading assessment phase plan', () => {
  test('keeps the assessment phase plan protected', async ({ request }) => {
    const response = await request.get('/api/speed-reading/assessment/phase-plan');

    expect(response.status()).toBe(401);
  });

  test('returns the four-phase plan for an authenticated user', async ({ request }) => {
    test.skip(
      !email || !password,
      'Set E2E_SPEED_READING_EMAIL/E2E_SPEED_READING_PASSWORD or admin credentials for the authenticated read.'
    );

    const loginResponse = await request.post('/api/auth/login', {
      data: { email, password }
    });

    expect(loginResponse.status()).toBe(200);
    const login = await completeApiMfa(request, await loginResponse.json());
    expect(login.accessToken).toEqual(expect.any(String));

    const response = await request.get('/api/speed-reading/assessment/phase-plan', {
      headers: { Authorization: `Bearer ${login.accessToken}` }
    });

    expect(response.status()).toBe(200);
    const body = await response.json();
    const plan = body.data ?? body;
    expect(plan.phases).toHaveLength(4);
    expect(plan.phases.map(phase => phase.phase)).toEqual([1, 2, 3, 4]);
    expect([1, 2, 3, 4]).toContain(plan.nextPhase);
  });

  test('rejects out-of-order focus trials and actions submitted while paused', async ({ request }) => {
    test.skip(
      !email || !password || !focusExerciseId || !runsSpeedReadingWrite || !isDisposable,
      'Requires disposable write mode, student credentials, and E2E_SPEED_READING_FOCUS_EXERCISE_ID.'
    );

    const headers = await authenticatedHeaders(request);
    const startResponse = await request.post('/api/speed-reading/exercise-sessions/start', {
      headers,
      data: { exerciseId: focusExerciseId }
    });
    expect(startResponse.status()).toBe(200);
    const start = await startResponse.json();
    const session = start.data ?? start;

    const focusStart = await request.post(
      `/api/speed-reading/exercise-sessions/${session.sessionId}/validate`,
      { headers, data: { actionId: crypto.randomUUID(), action: 'focus_start' } }
    );
    expect(focusStart.status()).toBe(200);
    expect((await focusStart.json()).isValid).toBe(true);

    const skippedTrial = await request.post(
      `/api/speed-reading/exercise-sessions/${session.sessionId}/validate`,
      {
        headers,
        data: {
          actionId: crypto.randomUUID(),
          action: 'focus_step',
          index: 1,
          timestamp: '2099-01-01T00:00:00Z'
        }
      }
    );
    expect(skippedTrial.status()).toBe(200);
    expect((await skippedTrial.json()).isValid).toBe(false);

    const pause = await request.post(
      `/api/speed-reading/exercise-sessions/${session.sessionId}/pause`,
      { headers }
    );
    expect(pause.status()).toBe(200);
    const whilePaused = await request.post(
      `/api/speed-reading/exercise-sessions/${session.sessionId}/validate`,
      { headers, data: { actionId: crypto.randomUUID(), action: 'focus_step', index: 0 } }
    );
    expect(whilePaused.status()).not.toBe(200);

    const resume = await request.post(
      `/api/speed-reading/exercise-sessions/${session.sessionId}/resume`,
      { headers }
    );
    expect(resume.status()).toBe(200);
  });
});

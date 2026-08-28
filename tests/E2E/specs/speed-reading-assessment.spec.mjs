import { test, expect } from '@playwright/test';
import { completeApiMfa } from '../support/totp.mjs';

const email = process.env.E2E_SPEED_READING_EMAIL ?? process.env.E2E_ADMIN_EMAIL;
const password = process.env.E2E_SPEED_READING_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD;

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
});

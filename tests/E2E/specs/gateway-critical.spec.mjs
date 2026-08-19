import { test, expect } from '@playwright/test';

const protectedEndpoints = [
  ['/api/users/summary', 'GET'],
  ['/api/institutions', 'GET'],
  ['/api/support/requests', 'GET'],
  ['/api/email-templates', 'GET'],
  ['/api/coaching-admin/overview', 'GET']
];

test.describe('Gateway critical API flows', () => {
  test('exposes a healthy gateway to the public edge', async ({ request }) => {
    const response = await request.get('/health');

    expect(response.status()).toBe(200);
    expect(await response.text()).toContain('Healthy');
  });

  test('keeps authentication bootstrap routes anonymous', async ({ request }) => {
    const response = await request.post('/api/auth/login', {
      data: { email: '', password: '' }
    });

    expect([400, 422, 429]).toContain(response.status());
    expect(response.status()).not.toBe(401);
    expect(response.status()).not.toBe(403);
  });

  test('keeps public support validation reachable without authentication', async ({ request }) => {
    const response = await request.post('/api/support/submit', { data: {} });

    expect([400, 422, 429]).toContain(response.status());
    expect(response.status()).not.toBe(401);
    expect(response.status()).not.toBe(403);
  });

  test('does not expose support replies anonymously', async ({ request }) => {
    const response = await request.post('/api/support/reply', { data: {} });

    expect(response.status()).toBe(401);
  });

  for (const [endpoint, method] of protectedEndpoints) {
    test(`${method} ${endpoint} requires an access token`, async ({ request }) => {
      const response = method === 'GET'
        ? await request.get(endpoint)
        : await request.fetch(endpoint, { method });

      expect(response.status()).toBe(401);
    });
  }
});

test.describe('Gateway authenticated admin surface', () => {
  test.skip(
    !process.env.E2E_ADMIN_EMAIL || !process.env.E2E_ADMIN_PASSWORD,
    'Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to run authenticated admin checks.'
  );

  test('logs in through the gateway and reads every enabled admin surface', async ({ request }) => {
    const loginResponse = await request.post('/api/auth/login', {
      data: {
        email: process.env.E2E_ADMIN_EMAIL,
        password: process.env.E2E_ADMIN_PASSWORD
      }
    });

    expect(loginResponse.status()).toBe(200);
    const login = await loginResponse.json();
    expect(login.accessToken).toEqual(expect.any(String));
    expect(login.refreshToken).toEqual(expect.any(String));

    const refreshResponse = await request.post('/api/auth/refresh-token', {
      data: { refreshToken: login.refreshToken }
    });
    expect(refreshResponse.status()).toBe(200);
    const refreshed = await refreshResponse.json();
    expect(refreshed.accessToken).toEqual(expect.any(String));

    const token = refreshed.accessToken;
    for (const endpoint of protectedEndpoints) {
      const response = await request.get(endpoint, {
        headers: { Authorization: `Bearer ${token}` }
      });
      expect(response.status(), endpoint).toBe(200);
    }
  });
});

test.describe('Gateway disposable support write flow', () => {
  test.skip(
    process.env.E2E_RUN_SUPPORT_WRITE !== 'true',
    'Set E2E_RUN_SUPPORT_WRITE=true only for a disposable environment.'
  );

  test('returns the original support request id for an equivalent retry', async ({ request }) => {
    const nonce = crypto.randomUUID();
    const payload = {
      firstName: 'E2E',
      lastName: 'Support',
      email: `e2e-support-${nonce}@example.test`,
      subject: 'Disposable idempotency verification',
      message: 'This request is created only by the disposable E2E environment.'
    };
    const headers = { 'Idempotency-Key': `e2e-support-${nonce}` };

    const first = await request.post('/api/support/submit', { data: payload, headers });
    expect(first.status()).toBe(200);
    const supportRequestId = await first.json();
    expect(supportRequestId).toEqual(expect.any(String));

    const retry = await request.post('/api/support/submit', { data: payload, headers });
    expect(retry.status()).toBe(200);
    expect(await retry.json()).toBe(supportRequestId);
  });
});

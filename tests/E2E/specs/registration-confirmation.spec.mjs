import { test, expect } from '@playwright/test';

const mailApiBaseUrl = process.env.E2E_MAILCATCHER_API_BASE_URL?.replace(/\/$/, '');

function verificationParamsFromMessage(text) {
  const match = text.match(
    /\/auth\/confirm-email\?token=([^&"'<>\s]+)(?:&amp;|&)userId=([^"'<>\s]+)/i
  );
  if (!match) return null;

  const token = decodeURIComponent(match[1]);
  const userId = decodeURIComponent(match[2]);
  return { token, userId };
}

async function waitForVerificationLink(request, recipient) {
  const deadline = Date.now() + 60_000;

  while (Date.now() < deadline) {
    const listResponse = await request.get(`${mailApiBaseUrl}/messages`);
    expect(listResponse.ok()).toBeTruthy();
    const messages = await listResponse.json();

    for (const message of messages) {
      const summary = JSON.stringify(message);
      if (!summary.toLowerCase().includes(recipient.toLowerCase())) continue;

      // MailCatcher's JSON endpoint returns metadata only. The verification
      // URL lives in the rendered HTML message body.
      const detailResponse = await request.get(`${mailApiBaseUrl}/messages/${message.id}.html`);
      if (!detailResponse.ok()) continue;

      const params = verificationParamsFromMessage(await detailResponse.text());
      if (params) return params;
    }

    await new Promise(resolve => setTimeout(resolve, 1_000));
  }

  throw new Error('Verification e-mail was not observed before the disposable E2E deadline.');
}

test.describe('Disposable registration and email confirmation', () => {
  test.skip(
    process.env.E2E_RUN_REGISTRATION !== 'true',
    'Set E2E_RUN_REGISTRATION=true only for a disposable environment.'
  );

  test('registers a student and confirms the e-mail link from MailCatcher', async ({ request }) => {
    test.setTimeout(90_000);
    const nonce = crypto.randomUUID();
    const email = `e2e-student-${nonce}@example.test`;

    const registration = await request.post('/api/auth/register/student', {
      data: {
        email,
        password: 'E2e-Disposable-Password-123!',
        firstName: 'E2E',
        lastName: 'Student',
        phone: null
      }
    });

    expect(registration.status()).toBe(200);
    const registrationBody = await registration.json();
    expect(registrationBody.userId).toEqual(expect.any(String));

    const verificationParams = await waitForVerificationLink(request, email);
    expect(registrationBody.userId).toBe(verificationParams.userId);

    const confirmation = await request.post('/api/auth/confirm-email', {
      data: {
        userId: verificationParams.userId,
        token: verificationParams.token
      }
    });

    expect(confirmation.status()).toBe(200);
  });
});

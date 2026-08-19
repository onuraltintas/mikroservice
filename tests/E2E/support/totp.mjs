import { createHmac } from 'node:crypto';

const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

export function generateTotp(secret, timestamp = Date.now()) {
  const key = decodeBase32(secret);
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(Math.floor(timestamp / 1000 / 30)));
  const digest = createHmac('sha1', key).update(counter).digest();
  const offset = digest[digest.length - 1] & 0x0f;
  const value = (digest.readUInt32BE(offset) & 0x7fffffff) % 1_000_000;
  return value.toString().padStart(6, '0');
}

export async function completeApiMfa(request, login) {
  if (!login.requiresMfa) return login;

  let secret = process.env.E2E_ADMIN_TOTP_SECRET;
  if (login.mfaEnrollmentRequired) {
    const setupResponse = await request.post('/api/auth/mfa/setup', {
      data: { challengeToken: login.mfaChallengeToken }
    });
    if (!setupResponse.ok()) throw new Error(`MFA setup failed with ${setupResponse.status()}`);
    const setup = await setupResponse.json();
    secret = setup.secret;
    const enableResponse = await request.post('/api/auth/mfa/enable', {
      data: {
        challengeToken: login.mfaChallengeToken,
        setupToken: setup.setupToken,
        code: generateTotp(secret)
      }
    });
    if (!enableResponse.ok()) throw new Error(`MFA enable failed with ${enableResponse.status()}`);
    return enableResponse.json();
  }

  if (!secret) {
    throw new Error('E2E_ADMIN_TOTP_SECRET is required for an enrolled SystemAdmin.');
  }

  const verifyResponse = await request.post('/api/auth/mfa/verify', {
    data: {
      challengeToken: login.mfaChallengeToken,
      code: generateTotp(secret),
      recoveryCode: null
    }
  });
  if (!verifyResponse.ok()) throw new Error(`MFA verification failed with ${verifyResponse.status()}`);
  return verifyResponse.json();
}

function decodeBase32(secret) {
  const normalized = secret.replaceAll(' ', '').toUpperCase();
  let bits = 0;
  let value = 0;
  const bytes = [];
  for (const character of normalized) {
    const index = alphabet.indexOf(character);
    if (index < 0) throw new Error('E2E TOTP secret is not valid Base32.');
    value = (value << 5) | index;
    bits += 5;
    if (bits >= 8) {
      bytes.push((value >>> (bits - 8)) & 0xff);
      bits -= 8;
      value &= (1 << bits) - 1;
    }
  }
  return Buffer.from(bytes);
}

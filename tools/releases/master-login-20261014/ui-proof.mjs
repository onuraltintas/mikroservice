import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const origin = process.env.LOGIN_UI_ORIGIN ?? 'http://127.0.0.1:4266';
assert.ok(['http://127.0.0.1:4266', 'https://masterhizliokuma.com'].includes(origin));
const output = origin.startsWith('https:') ? 'artifacts/master-login-live-ui' : 'artifacts/master-login-ui';
const root = resolve(fileURLToPath(new URL('../../../.tmp/master-registration-source-20261014/clients/speed-reading/dist/speed-reading-frontend/browser/', import.meta.url)));
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.json': 'application/json', '.woff2': 'font/woff2', '.png': 'image/png' };
let server;
if (!origin.startsWith('https:')) {
  server = createServer(async (request, response) => {
    try {
      const file = resolve(root, '.' + decodeURIComponent(new URL(request.url, origin).pathname));
      if (file !== root && !file.startsWith(root + sep)) { response.writeHead(403); response.end(); return; }
      const target = extname(file) ? file : resolve(root, 'index.html');
      response.writeHead(200, { 'Content-Type': types[extname(target)] ?? 'application/octet-stream' }); response.end(await readFile(target));
    } catch { response.writeHead(404); response.end(); }
  });
  await new Promise(resolve => server.listen(4266, '127.0.0.1', resolve));
}
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true }); const results = [];
try {
  for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }, { width: 320, height: 740 }]) {
    for (const mode of ['password', 'mfa', 'setup', 'google']) {
      const page = await browser.newPage({ viewport, serviceWorkers: 'block' }); const writes = [], errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.google = { accounts: { id: {
          initialize: options => { window.__googleLogin = options.callback; },
          renderButton: element => { const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Google ile giriş yap'; button.onclick = () => window.__googleLogin({ credential: 'fixture-google' }); element.replaceChildren(button); }
        } } };
        window.grecaptcha = { ready: callback => callback(), execute: async () => 'fixture-captcha-token' };
      });
      await page.route('**/api/**', async route => {
        const request = route.request(), path = new URL(request.url()).pathname;
        if (path.endsWith('/captcha-config')) return route.fulfill({ json: { enabled: true, siteKey: 'fixture-site-key' } });
        if (path.includes('/platform/legal-pages/')) { const slug = path.split('/').at(-1); return route.fulfill({ json: { success: true, data: { slug, title: slug, content: 'Published fixture', isPublished: true, version: 3 } } }); }
        if (request.method() !== 'GET') writes.push({ path, body: request.postDataJSON(), captcha: request.headers()['x-auth-recaptcha-token'] });
        if (path.endsWith('/speed-reading/login')) {
          if (mode === 'mfa' || mode === 'setup') return route.fulfill({ json: { requiresMfa: true, mfaChallengeToken: 'fixture-challenge', mfaEnrollmentRequired: mode === 'setup' } });
          return route.fulfill({ status: 400, json: { message: 'E-posta adresinizi doğrulayın.' } });
        }
        if (path.endsWith('/resend-verification-email')) return route.fulfill({ json: {} });
        if (path.endsWith('/mfa/setup')) return route.fulfill({ json: { secret: 'FIXTURE-MFA-SECRET', setupToken: 'fixture-setup', otpAuthUri: 'otpauth://totp/fixture', challengeToken: 'fixture-challenge' } });
        if (path.endsWith('/mfa/enable')) {
          const token = 'e30.' + Buffer.from(JSON.stringify({ sub: 'fixture-user', role: 'Student', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url') + '.fixture';
          return route.fulfill({ json: { accessToken: token, tokenType: 'Bearer', expiresInMinutes: 15, recoveryCodes: ['FIXTURE-RECOVERY-ONE', 'FIXTURE-RECOVERY-TWO'] } });
        }
        if (path.endsWith('/mfa/verify')) return route.fulfill({ status: 400, json: { message: 'Fixture: doğrulama kodu geçersiz.' } });
        if (path.endsWith('/speed-reading/google-login')) return route.fulfill({ json: { requiresLegalAcceptance: true, registrationToken: 'fixture-registration' } });
        if (path.endsWith('/google-register-complete')) return route.fulfill({ status: 409, json: { message: 'Fixture: kayıt tamamlanamadı.' } });
        if (request.method() !== 'GET') return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
        return route.fulfill({ json: {} });
      });
      await page.goto(origin + '/auth/login?returnUrl=' + encodeURIComponent('/auth/accept-invitation?token=fixture'));
      await page.getByRole('heading', { name: 'Tekrar hoş geldin.', exact: true }).waitFor();
      assert.equal(await page.locator('h1').count(), 1); assert.equal(await page.getByRole('navigation', { name: 'Kayıt türü' }).count(), 0);
      const login = page.locator('form[aria-busy]').first(), submit = login.locator('button[type="submit"]');
      assert.equal(await submit.isDisabled(), true);
      const email = login.locator('[formControlName="email"]'), password = login.locator('[formControlName="password"]');
      assert.equal(await email.getAttribute('autocomplete'), 'username'); assert.equal(await password.getAttribute('autocomplete'), 'current-password');
      assert.ok((await page.getByRole('link', { name: 'Öğretmen kaydı', exact: false }).getAttribute('href')).includes('returnUrl='));
      assert.ok((await page.getByRole('link', { name: 'Şifremi Unuttum?', exact: true }).getAttribute('href')).includes('/auth/forgot-password'));
      await email.fill('login-fixture@example.invalid'); await password.fill('FixturePassword7!');
      await page.getByRole('button', { name: 'Şifreyi göster', exact: true }).click(); await login.locator('input[type="text"][formControlName="password"]').waitFor();
      await page.getByRole('button', { name: 'Şifreyi gizle', exact: true }).click(); await login.locator('input[type="password"]').waitFor();
      await page.evaluate(() => { window.scrollTo(0, 0); document.body.scrollTop = 0; });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
      if (mode === 'password') {
        await page.screenshot({ path: output + '/login-' + viewport.width + '.png' });
        await page.mouse.move(viewport.width * .8, viewport.height * .8); await page.mouse.wheel(0, 4000);
        await page.waitForFunction(() => { const r = document.querySelector('.register-options').getBoundingClientRect(); return r.bottom <= innerHeight && r.bottom > 0; });
        await page.getByRole('checkbox', { name: 'Beni Hatırla' }).check(); await submit.click();
        await page.getByRole('status').filter({ hasText: 'E-posta adresiniz doğrulanmadı.' }).waitFor();
        await page.getByRole('button', { name: 'Doğrulama E-postasını Tekrar Gönder' }).click();
        await login.locator('button[type="submit"]:enabled').waitFor();
        assert.equal(writes.find(write => write.path.endsWith('/speed-reading/login')).body.rememberMe, true);
        assert.equal(writes.find(write => write.path.endsWith('/speed-reading/login')).captcha, 'fixture-captcha-token');
        assert.equal(writes.find(write => write.path.endsWith('/resend-verification-email')).body.email, 'login-fixture@example.invalid');
      } else if (mode === 'google') {
        await page.getByRole('button', { name: 'Google ile giriş yap', exact: true }).click();
        const region = page.getByRole('region', { name: 'Google kaydını tamamlayın' }); await region.waitFor();
        assert.equal(await login.isVisible(), false);
        const confirm = region.getByRole('button', { name: 'Onayla ve hesabımı oluştur', exact: true }); assert.equal(await confirm.isDisabled(), true);
        for (const check of await region.getByRole('checkbox').all()) await check.check();
        await region.locator('button:enabled').first().waitFor(); await confirm.click();
        await region.getByRole('alert').filter({ hasText: 'Fixture: kayıt tamamlanamadı.' }).waitFor();
        const write = writes.find(write => write.path.endsWith('/google-register-complete')); assert.equal(write.body.registrationToken, 'fixture-registration'); assert.equal(write.body.legalAcceptances.length, 3);
        await region.getByRole('button', { name: 'Vazgeç', exact: true }).click(); await login.waitFor(); assert.equal(await email.inputValue(), 'login-fixture@example.invalid');
      } else {
        await submit.click();
        const heading = mode === 'setup' ? 'İki adımlı doğrulamayı etkinleştir' : 'İki adımlı doğrulama';
        await page.getByRole('heading', { name: heading, exact: true }).waitFor();
        const mfa = page.locator('form.mfa-login-panel'); const primary = mfa.locator('button[type="submit"]');
        assert.equal(await primary.isDisabled(), true); await mfa.locator('[name="mfaCode"]').fill('123456'); await mfa.locator('button[type="submit"]:enabled').waitFor(); await primary.click();
        if (mode === 'setup') {
          await page.getByRole('heading', { name: 'İki adımlı doğrulama etkinleştirildi', exact: true }).waitFor();
          assert.equal(await page.locator('.mfa-recovery-codes code').count(), 2); assert.equal(writes.find(write => write.path.endsWith('/mfa/enable')).body.setupToken, 'fixture-setup');
        } else {
          await mfa.getByRole('alert').filter({ hasText: 'Fixture: doğrulama kodu geçersiz.' }).waitFor();
          await mfa.getByRole('button', { name: 'Kurtarma kodu kullan', exact: true }).click(); await mfa.locator('[name="mfaRecoveryCode"]').fill('fixture-recovery'); await mfa.locator('button[type="submit"]:enabled').waitFor(); await primary.click();
          await mfa.getByRole('alert').filter({ hasText: 'Fixture: doğrulama kodu geçersiz.' }).waitFor();
          assert.ok(writes.some(write => write.path.endsWith('/mfa/verify') && write.body.code === null && write.body.recoveryCode === 'fixture-recovery'));
        }
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: output + '/' + mode + '-' + viewport.width + '.png' });
      }
      assert.deepEqual(errors, []); results.push(mode + '-' + viewport.width); await page.close();
    }
  }
  await writeFile(output + '/results.json', JSON.stringify({ passed: results.length, checks: results }, null, 2)); console.log(JSON.stringify({ passed: results.length, checks: results }));
} finally { await browser.close(); if (server) await new Promise(resolve => server.close(resolve)); }

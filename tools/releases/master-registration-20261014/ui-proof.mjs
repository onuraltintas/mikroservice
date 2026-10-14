import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const origin = process.env.REGISTRATION_UI_ORIGIN ?? 'http://127.0.0.1:4266';
assert.ok(['http://127.0.0.1:4266', 'https://masterhizliokuma.com'].includes(origin));
const output = origin.startsWith('https:') ? 'artifacts/master-registration-live-ui' : 'artifacts/master-registration-ui';
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
const accounts = [
  { role: 'student', path: 'register', title: 'Öğrenci hesabını oluştur' },
  { role: 'teacher', path: 'register-teacher', title: 'Öğretmen hesabını oluştur' },
  { role: 'institution', path: 'register-school', title: 'Kurum hesabını oluştur' }
];
try {
  for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }, { width: 320, height: 740 }]) {
    for (const account of accounts) {
      const page = await browser.newPage({ viewport, serviceWorkers: 'block' }); const errors = [], writes = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.google = { accounts: { id: { initialize: () => {}, renderButton: element => { element.innerHTML = '<button type="button">Google ile kayıt ol</button>'; } } } };
        window.grecaptcha = { ready: callback => callback(), execute: async (_key, options) => { window.__captchaAction = options.action; return 'fixture-captcha-token'; } };
      });
      // Every API request is intercepted; no accounts or registrations are written to production.
      await page.route('**/api/**', async route => {
        const request = route.request(); const path = new URL(request.url()).pathname;
        if (path.endsWith('/captcha-config')) return route.fulfill({ json: { enabled: true, siteKey: 'fixture-site-key' } });
        if (path.includes('/platform/legal-pages/')) {
          const slug = path.split('/').at(-1);
          return route.fulfill({ json: { success: true, data: { slug, title: slug === 'privacy' ? 'Gizlilik politikası' : slug === 'kvkk' ? 'KVKK aydınlatma metni' : 'Hızlı Okuma kullanım koşulları', content: 'Published fixture', isPublished: true, version: 3 } } });
        }
        if (path.endsWith('/locations/provinces')) return route.fulfill({ json: [{ id: 'province-fixture', name: 'İstanbul' }] });
        if (path.endsWith('/districts')) return route.fulfill({ json: [{ id: 'district-fixture', provinceId: 'province-fixture', name: 'Kadıköy' }] });
        if (request.method() !== 'GET') {
          writes.push({ path, body: request.postDataJSON(), captcha: request.headers()['x-auth-recaptcha-token'] });
          return route.fulfill({ status: 409, json: { message: 'Fixture: bu e-posta zaten kullanılıyor.' } });
        }
        await route.fulfill({ json: {} });
      });
      await page.goto(origin + '/auth/' + account.path + '?returnUrl=' + encodeURIComponent('/auth/accept-invitation?token=fixture'));
      await page.getByRole('heading', { name: account.title, exact: true }).waitFor();
      const favicon = page.locator('link[rel="icon"]'); assert.equal(await favicon.count(), 1);
      assert.equal(await favicon.getAttribute('href'), 'brand/master-favicon.svg');
      const iconResponse = await page.request.get(origin + '/brand/master-favicon.svg');
      assert.equal(iconResponse.status(), 200); assert.ok((await iconResponse.text()).includes('ONAL Yazılım ve Otomasyon işareti'));
      assert.equal(await page.locator('h1').count(), 1);
      const nav = page.getByRole('navigation', { name: 'Kayıt türü' });
      assert.equal(await nav.locator('[aria-current="page"]').count(), 1);
      assert.ok((await nav.getByRole('link', { name: 'Öğretmen', exact: true }).getAttribute('href')).includes('returnUrl='));
      const form = page.locator('mat-card-content > form').first(), submit = form.locator('button[type="submit"]');
      assert.equal(await submit.isDisabled(), true);
      const fill = (name, value) => form.locator('[formControlName="' + name + '"]').fill(value);
      await fill('firstName', 'Test'); await fill('lastName', 'Kullanıcı');
      await fill(account.role === 'institution' ? 'adminEmail' : 'email', 'design-fixture@example.invalid');
      await fill('password', 'FixtureStrong7!'); await fill('confirmPassword', 'FixtureStrong7!');
      if (account.role === 'institution') {
        await fill('schoolName', 'Fixture Eğitim Kurumu'); await fill('contactEmail', 'institution@example.invalid'); await fill('phoneNumber', '05550001122');
        await form.locator('[formControlName="provinceId"]').click(); await page.getByRole('option', { name: 'İstanbul', exact: true }).click();
        await form.locator('[formControlName="districtId"]').click(); await page.getByRole('option', { name: 'Kadıköy', exact: true }).click();
      }
      assert.equal(await submit.isDisabled(), true);
      const checks = form.locator('.legal-consent-row input'); assert.equal(await checks.count(), 3);
      for (const check of await checks.all()) await check.check();
      await form.locator('button[type="submit"]:enabled').waitFor();
      assert.equal(await submit.isEnabled(), true);
      await fill('confirmPassword', 'Different7!'); await form.locator('[formControlName="confirmPassword"]').blur(); await form.locator('button[type="submit"]:disabled').waitFor(); assert.equal(await submit.isDisabled(), true);
      await fill('confirmPassword', 'FixtureStrong7!');
      await form.getByRole('button', { name: 'Parolayı göster veya gizle', exact: true }).click();
      await form.locator('[formControlName="password"][type="text"]').waitFor();
      await form.getByRole('button', { name: 'Parolayı göster veya gizle', exact: true }).click();
      await form.locator('[formControlName="password"][type="password"]').waitFor();
      await page.getByRole('button', { name: 'Mevcut hesabımla katıl', exact: true }).click();
      await page.getByRole('heading', { name: 'Mevcut hesabınla katıl', exact: true }).waitFor();
      await page.getByRole('button', { name: 'Yeni hesap oluştur', exact: true }).click();
      await form.waitFor({ state: 'visible' });
      assert.equal(await form.locator('[formControlName="firstName"]').inputValue(), 'Test');
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
      await page.evaluate(() => { window.scrollTo(0, 0); document.body.scrollTop = 0; });
      await page.screenshot({ path: output + '/' + account.role + '-' + viewport.width + '.png' });
      await page.mouse.move(viewport.width * .8, viewport.height * .8); await page.mouse.wheel(0, 4000);
      await page.waitForFunction(() => {
        const link = document.querySelector('mat-card-content > form .login-redirect, mat-card-content > form .login-link');
        const rect = link.getBoundingClientRect(); return rect.top >= 0 && rect.bottom <= innerHeight;
      });
      await page.screenshot({ path: output + '/' + account.role + '-' + viewport.width + '-bottom.png' });
      await submit.click();
      try { await page.getByRole('alert').filter({ hasText: 'Fixture: bu e-posta zaten kullanılıyor.' }).waitFor(); }
      catch (error) { console.log(JSON.stringify({ failed: account.role, url: page.url(), paths: writes.map(write => write.path), pageErrors: errors, alerts: await page.getByRole('alert').allTextContents(), disabled: await submit.isDisabled() })); throw error; }
      assert.equal(writes.length, 1); assert.equal(writes[0].path, '/api/auth/speed-reading/register/' + account.role);
      assert.equal(writes[0].captcha, 'fixture-captcha-token'); assert.equal(writes[0].body.legalAcceptances.length, 3);
      await form.locator('button[type="submit"]:enabled').waitFor(); assert.equal(await submit.isEnabled(), true); assert.deepEqual(errors, []);
      assert.equal(await page.evaluate(() => window.__captchaAction), 'auth_register');
      results.push(account.role + '-' + viewport.width + '-form-consents-join-errors-captcha'); await page.close();
    }
  }
  await writeFile(output + '/results.json', JSON.stringify({ passed: results.length, checks: results }, null, 2)); console.log(JSON.stringify({ passed: results.length, checks: results }));
} finally { await browser.close(); if (server) await new Promise(resolve => server.close(resolve)); }

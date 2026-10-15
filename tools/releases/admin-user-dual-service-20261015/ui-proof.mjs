import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const origin = process.env.ADMIN_USER_UI_ORIGIN ?? 'http://127.0.0.1:4201';
assert.ok(['http://127.0.0.1:4201', 'https://eduivme.com'].includes(origin));
const output = 'artifacts/admin-user-dual-service-' + (origin.startsWith('https:') ? 'live' : 'local');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
try {
  for (const mobile of [false, true]) {
    const context = await browser.newContext({ serviceWorkers: 'block', viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 } });
    const page = await context.newPage(); page.setDefaultTimeout(15000);
    const errors = [], writes = []; let rejectOnce = false;
    page.on('pageerror', error => errors.push(error.message));
    // No live account, credential, email invitation or other API mutation is performed.
    await context.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      if (path === '/api/auth/refresh-token') {
        const payload = { sub: 'create-user-fixture', role: 'SystemAdmin', amr: 'mfa', permission: ['Permissions.Users.View', 'Permissions.Users.Create', 'Permissions.Users.Edit'] };
        return route.fulfill({ json: { accessToken: 'e30.' + Buffer.from(JSON.stringify(payload)).toString('base64url') + '.fixture', tokenType: 'Bearer', expiresInMinutes: 15 } });
      }
      if (path === '/api/users' && request.method() === 'POST') {
        writes.push(request.postDataJSON());
        if (rejectOnce) { rejectOnce = false; return route.fulfill({ status: 400, json: { error: { code: 'Test.Retry', description: 'Test: yeniden deneyin.' } } }); }
        return route.fulfill({ json: { userId: 'new-user-fixture' } });
      }
      if (request.method() !== 'GET') return route.fulfill({ status: 500, json: { message: 'Unexpected fixture mutation' } });
      const roles = ['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner', 'SystemAdmin'];
      const data = path === '/api/users' ? { items: [], totalCount: 0, pageNumber: 1, pageSize: 10 }
        : path === '/api/users/roles' ? roles : path === '/api/roles' ? roles.map(name => ({ id: name, name })) : [];
      return route.fulfill({ json: data });
    });
    await page.goto(origin + '/dashboard/identity/users');
    for (const role of ['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner']) {
      for (const products of [[1], [2], [1, 2]]) {
        const product = products.length === 1 ? products[0] : null;
        const service = products.map(p => role + ' · ' + (p === 1 ? 'Koçluk' : 'Hızlı Okuma')).join(' / ');
        await page.getByRole('button', { name: 'Kullanıcı Ekle', exact: true }).click();
        const modal = page.getByRole('dialog');
        await modal.locator('input[formControlName="firstName"]').fill('Servis');
        await modal.locator('input[formControlName="lastName"]').fill('Test');
        await modal.locator('input[formControlName="email"]').fill('new-user@example.test');
        await modal.getByLabel('Kullanıcı Rolü', { exact: true }).selectOption(role);
        const countBefore = writes.length;
        await modal.getByRole('button', { name: 'Kullanıcı Oluştur', exact: true }).click();
        await modal.getByText('En az bir servis seçin. İkisini birlikte seçebilirsiniz.', { exact: true }).waitFor();
        assert.equal(writes.length, countBefore);
        await modal.getByRole('checkbox', { name: 'Koçluk', exact: true }).setChecked(products.includes(1));
        await modal.getByRole('checkbox', { name: 'Hızlı Okuma', exact: true }).setChecked(products.includes(2));
        await page.screenshot({ path: output + '/' + role + '-' + product + '-' + (mobile ? 'mobile' : 'desktop') + '.png', fullPage: true });
        rejectOnce = role === 'Teacher' && product === null;
        const shouldRetry = rejectOnce;
        await modal.getByRole('button', { name: 'Kullanıcı Oluştur', exact: true }).click();
        if (shouldRetry) {
          await modal.getByText('Test: yeniden deneyin.', { exact: true }).waitFor();
          assert.ok(await modal.getByRole('checkbox', { name: 'Koçluk', exact: true }).isChecked());
          assert.ok(await modal.getByRole('checkbox', { name: 'Hızlı Okuma', exact: true }).isChecked());
          await modal.getByRole('button', { name: 'Kullanıcı Oluştur', exact: true }).click();
        }
        await modal.getByText(service, { exact: true }).waitFor();
        assert.equal(writes.at(-1).product, product); assert.equal(writes.at(-1).role, role);
        assert.deepEqual(writes.at(-1).products, products);
        assert.equal(writes.length - countBefore, shouldRetry ? 2 : 1);
        await modal.getByRole('button', { name: 'Tamam, Kapat', exact: true }).click();
        results.push((mobile ? 'mobile' : 'desktop') + '-' + role + '-' + product + '-required-payload-success' + (shouldRetry ? '-retry' : ''));
      }
    }
    await page.getByRole('button', { name: 'Kullanıcı Ekle', exact: true }).click();
    const modal = page.getByRole('dialog');
    await modal.locator('input[formControlName="firstName"]').fill('Global');
    await modal.locator('input[formControlName="lastName"]').fill('Admin');
    await modal.locator('input[formControlName="email"]').fill('global@example.test');
    await modal.getByRole('checkbox', { name: 'Koçluk', exact: true }).check();
    await modal.getByRole('checkbox', { name: 'Hızlı Okuma', exact: true }).check();
    await modal.getByLabel('Kullanıcı Rolü', { exact: true }).selectOption('SystemAdmin');
    assert.equal(await modal.getByRole('checkbox').count(), 0);
    await modal.getByRole('button', { name: 'Kullanıcı Oluştur', exact: true }).click();
    await modal.getByText('SystemAdmin · Tüm servisler', { exact: true }).waitFor();
    assert.equal(writes.at(-1).product, null);
    assert.deepEqual(writes.at(-1).products, []);
    assert.deepEqual(errors, []);
    results.push((mobile ? 'mobile' : 'desktop') + '-SystemAdmin-global');
    await context.close();
  }
  await writeFile(output + '/results.json', JSON.stringify({ passed: results.length, results }, null, 2));
  console.log(JSON.stringify({ passed: results.length, output }));
} finally { await browser.close(); }

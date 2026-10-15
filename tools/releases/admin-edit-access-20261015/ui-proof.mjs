import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const origin = process.env.ADMIN_EDIT_ACCESS_ORIGIN ?? 'http://127.0.0.1:4202';
assert.ok(['http://127.0.0.1:4202', 'https://eduivme.com'].includes(origin));
const output = 'artifacts/admin-edit-access-' + (origin.startsWith('https:') ? 'live' : 'local');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
try {
  for (const mobile of [false, true]) {
    const context = await browser.newContext({ serviceWorkers: 'block', viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 } });
    const page = await context.newPage(); page.setDefaultTimeout(15000);
    const errors = [], writes = []; let failOnce = false, mfa = true, user;
    page.on('pageerror', error => errors.push(error.message));
    // All API calls are fixtures; live users, sessions and subscriptions are never mutated.
    await context.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname, method = request.method();
      if (path === '/api/auth/refresh-token') {
        const payload = { sub: 'access-admin-fixture', role: 'SystemAdmin', ...(mfa ? { amr: 'mfa' } : {}),
          permission: ['Permissions.Users.View', 'Permissions.Users.Edit'] };
        return route.fulfill({ json: { accessToken: 'e30.' + Buffer.from(JSON.stringify(payload)).toString('base64url') + '.fixture', tokenType: 'Bearer', expiresInMinutes: 15 } });
      }
      const access = path.match(/^\/api\/users\/access-fixture\/products\/(coaching|speed-reading)\/access$/);
      if (access && method === 'PUT') {
        writes.push({ path, body: request.postDataJSON() });
        if (failOnce) { failOnce = false; return route.fulfill({ status: 403, json: { message: 'Test: MFA doğrulamasını yenileyin.' } }); }
        const product = access[1], { isActive } = request.postDataJSON();
        user.productAccesses = [...user.productAccesses.filter(a => a.product !== product), { product, isActive }];
        return route.fulfill({ status: 204 });
      }
      if (method !== 'GET') return route.fulfill({ status: 500, json: { message: 'Unexpected fixture mutation' } });
      const roles = ['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner', 'SystemAdmin'];
      const data = path === '/api/users' ? { items: [user], totalCount: 1, pageNumber: 1, pageSize: 10 }
        : path === '/api/users/access-fixture' ? user : path === '/api/users/roles' ? roles
        : path === '/api/roles' ? roles.map(name => ({ id: name, name }))
        : path.includes('institution') ? { items: [], totalCount: 0 } : [];
      return route.fulfill({ json: data });
    });
    const open = async () => {
      await page.getByRole('button', { name: 'Menüyü aç', exact: true }).click();
      await page.getByRole('menuitem', { name: 'Düzenle', exact: true }).click();
      await page.getByRole('heading', { name: 'Servis erişimleri', exact: true }).waitFor();
      return page.getByRole('dialog');
    };
    const seed = role => ({ userId: 'access-fixture', firstName: 'Servis', lastName: 'Test', fullName: 'Servis Test', email: 'existing@example.test',
      role, roles: [role], isActive: true, emailConfirmed: true, permissions: [],
      productAccesses: [{ product: 'coaching', isActive: true }, { product: 'speed-reading', isActive: true }],
      productRoles: [{ role, product: 'coaching' }, { role, product: 'speed-reading' }] });
    for (const role of ['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner']) {
      user = seed(role);
      await page.goto(origin + '/dashboard/identity/users');
      let modal = await open();
      const assignments = structuredClone(user.productRoles);
      for (const [product, label] of [['coaching', 'Koçluk'], ['speed-reading', 'Hızlı Okuma']]) {
        const other = product === 'coaching' ? 'speed-reading' : 'coaching';
        const row = modal.locator(`[data-service="${product}"]`);
        const before = writes.length;
        failOnce = role === 'Teacher' && product === 'coaching';
        const retry = failOnce;
        await modal.getByRole('button', { name: label + ' erişimini kapat', exact: true }).click();
        if (retry) {
          await modal.getByText('Test: MFA doğrulamasını yenileyin.', { exact: true }).waitFor();
          await row.getByText('Erişim açık', { exact: true }).waitFor();
          await modal.getByRole('button', { name: label + ' erişimini kapat', exact: true }).click();
        }
        await row.getByText('Erişim kapalı', { exact: true }).waitFor();
        await modal.locator(`[data-service="${other}"]`).getByText('Erişim açık', { exact: true }).waitFor();
        assert.equal(writes.length - before, retry ? 2 : 1);
        assert.deepEqual(writes.at(-1), { path: `/api/users/access-fixture/products/${product}/access`, body: { isActive: false } });
        assert.deepEqual(user.productRoles, assignments);
        await page.screenshot({ path: `${output}/${role}-${product}-${mobile ? 'mobile' : 'desktop'}.png`, fullPage: true });
        await modal.getByRole('button', { name: 'Kapat', exact: true }).last().click();
        modal = await open();
        await modal.locator(`[data-service="${product}"]`).getByText('Erişim kapalı', { exact: true }).waitFor();
        await modal.getByRole('button', { name: label + ' erişimini aç', exact: true }).click();
        await modal.locator(`[data-service="${product}"]`).getByText('Erişim açık', { exact: true }).waitFor();
        assert.deepEqual(writes.at(-1).body, { isActive: true });
        results.push(`${mobile ? 'mobile' : 'desktop'}-${role}-${product}-revoke-reopen-persist${retry ? '-retry' : ''}`);
      }
      await modal.getByRole('button', { name: 'Kapat', exact: true }).last().click();
    }
    user = seed('SystemAdmin');
    await page.goto(origin + '/dashboard/identity/users');
    let modal = await open();
    await modal.getByText('SystemAdmin tüm servislerde genel yetkilidir; servis erişimiyle kısıtlanamaz.', { exact: true }).waitFor();
    assert.equal(await modal.locator('button[aria-label$="erişimini kapat"]').count(), 0);
    results.push(`${mobile ? 'mobile' : 'desktop'}-global-admin-explained`);
    await modal.getByRole('button', { name: 'Kapat', exact: true }).last().click();
    mfa = false; user = seed('Student');
    await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
    await page.goto(origin + '/dashboard/identity/users');
    modal = await open();
    await modal.getByText('Servis erişimlerini değiştirmek için MFA doğrulaması gerekiyor.', { exact: true }).waitFor();
    assert.equal(await modal.locator('button[aria-label$="erişimini kapat"]').count(), 0);
    results.push(`${mobile ? 'mobile' : 'desktop'}-mfa-required`);
    assert.deepEqual(errors, []);
    await context.close();
  }
  await writeFile(output + '/results.json', JSON.stringify({ passed: results.length, results }, null, 2));
  console.log(JSON.stringify({ passed: results.length, output }));
} finally { await browser.close(); }

import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const origin = process.env.ROLE_UI_ORIGIN ?? 'http://127.0.0.1:4199';
assert.ok(['http://127.0.0.1:4199', 'https://eduivme.com'].includes(origin));
const output = 'artifacts/admin-role-services-' + (origin.startsWith('https:') ? 'live' : 'local');
await mkdir(output, { recursive: true });
const results = [];
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  for (const mobile of [false, true]) {
    const context = await browser.newContext({ serviceWorkers: 'block', viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 } });
    const page = await context.newPage(); page.setDefaultTimeout(15000);
    const errors = [], writes = [];
    let failRemoval = false;
    const user = { userId: 'role-fixture', email: 'roles@example.test', fullName: 'Servis Rol Testi', role: 'Teacher', roles: ['Teacher', 'Teacher'],
      isActive: true, emailConfirmed: true, permissions: [], productAccesses: [{ product: 'coaching', isActive: true }, { product: 'speed-reading', isActive: true }],
      productRoles: [{ role: 'Teacher', product: 'coaching' }, { role: 'Teacher', product: 'speed-reading' }] };
    page.on('pageerror', error => errors.push(error.message));
    // Every API request is intercepted; real users, roles, credentials and permissions are never changed.
    await context.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname, method = request.method();
      if (path === '/api/users/bulk/coaching/role' && method === 'POST') {
        writes.push({ path, method, body: request.postDataJSON() });
        return route.fulfill({ json: { succeeded: 1, failed: 0, errors: [] } });
      }
      if (path === '/api/auth/refresh-token') {
        const payload = { sub: 'admin-fixture', email: 'admin@example.test', role: 'SystemAdmin', amr: 'mfa', permission: ['Permissions.Users.View', 'Permissions.Users.Edit'] };
        return route.fulfill({ json: { accessToken: 'e30.' + Buffer.from(JSON.stringify(payload)).toString('base64url') + '.fixture', tokenType: 'Bearer', expiresInMinutes: 15 } });
      }
      const roleRoute = path.match(/^\/api\/users\/role-fixture\/products\/(coaching|speed-reading)\/roles(?:\/([^/]+))?$/);
      if (roleRoute && ['POST', 'DELETE'].includes(method)) {
        writes.push({ path, method, body: request.postDataJSON() });
        if (method === 'DELETE' && failRemoval) return route.fulfill({ status: 403, json: { message: 'Test: rol kaldırma reddedildi.' } });
        const product = roleRoute[1], role = method === 'POST' ? request.postDataJSON().roleName : decodeURIComponent(roleRoute[2]);
        if (method === 'DELETE') user.productRoles = user.productRoles.filter(item => !(item.product === product && item.role === role));
        else user.productRoles.push({ role, product });
        user.roles = user.productRoles.map(item => item.role);
        return route.fulfill({ json: {} });
      }
      if (method !== 'GET') return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write: ' + path } });
      let data = [];
      if (path === '/api/users/roles') data = ['Student', 'Teacher', 'InstitutionAdmin', 'SystemAdmin'];
      else if (path === '/api/roles') data = ['Student', 'Teacher', 'InstitutionAdmin', 'SystemAdmin'].map(name => ({ id: name, name }));
      else if (path === '/api/users') data = { items: [user], totalCount: 1, pageNumber: 1, pageSize: 10 };
      else if (path === '/api/users/role-fixture') data = user;
      return route.fulfill({ json: data });
    });
    await page.goto(origin + '/dashboard/identity/users');
    const row = page.getByRole('row').filter({ hasText: user.email });
    await row.getByText('Teacher · Koçluk', { exact: true }).waitFor();
    await row.getByText('Teacher · Hızlı Okuma', { exact: true }).waitFor();
    await row.locator('button').last().click();
    await page.getByRole('menuitem', { name: 'Rol Yönetimi', exact: true }).click();
    const modal = page.getByRole('dialog', { name: 'Rol Yönetimi' });
    await modal.getByLabel('Rolün ekleneceği servis', { exact: true }).waitFor();
    assert.equal(await modal.getByRole('button', { name: /Student$/ }).isDisabled(), true);
    await modal.getByRole('button', { name: 'Teacher · Hızlı Okuma rolünü kaldır', exact: true }).click();
    await modal.getByRole('button', { name: 'Teacher · Hızlı Okuma rolünü kaldır', exact: true }).waitFor({ state: 'detached' });
    await modal.getByRole('button', { name: 'Teacher · Koçluk rolünü kaldır', exact: true }).waitFor();
    await modal.getByLabel('Rolün ekleneceği servis', { exact: true }).selectOption('speed-reading');
    await modal.getByRole('button', { name: /Teacher · Hızlı Okuma$/ }).click();
    await modal.getByRole('button', { name: 'Teacher · Hızlı Okuma rolünü kaldır', exact: true }).waitFor();
    failRemoval = true;
    await modal.getByRole('button', { name: 'Teacher · Koçluk rolünü kaldır', exact: true }).click();
    await page.getByText(/Bu işlem için MFA doğrulaması gerekiyor/).waitFor();
    assert.equal(await modal.getByRole('button', { name: 'Teacher · Koçluk rolünü kaldır', exact: true }).count(), 1);
    await page.screenshot({ path: output + '/roles-' + (mobile ? 'mobile' : 'desktop') + '.png', fullPage: true });
    await modal.getByRole('button', { name: 'Kapat', exact: true }).click();
    await row.getByText('Teacher · Koçluk', { exact: true }).waitFor();
    await row.getByText('Teacher · Hızlı Okuma', { exact: true }).waitFor();
    assert.deepEqual(writes.map(write => [write.method, write.path]), [
      ['DELETE', '/api/users/role-fixture/products/speed-reading/roles/Teacher'],
      ['POST', '/api/users/role-fixture/products/speed-reading/roles'],
      ['DELETE', '/api/users/role-fixture/products/coaching/roles/Teacher']
    ]);
    await page.goto(origin + '/dashboard/identity/bulk-users');
    await page.getByLabel(user.email + ' seç', { exact: true }).check();
    await page.getByLabel(/^Atanacak rol/).selectOption('Student');
    assert.equal(await page.getByRole('button', { name: 'Seçili kullanıcılara ata', exact: true }).isDisabled(), true);
    await page.getByLabel(/^Rolün ekleneceği servis/).selectOption('coaching');
    await page.getByLabel('Seçilen servisin mevcut rollerini kaldır', { exact: true }).check();
    await page.getByRole('button', { name: 'Seçili kullanıcılara ata', exact: true }).click();
    const confirmation = page.getByRole('dialog');
    await confirmation.getByText(/Student · Koçluk rolü uygulansın mı/).waitFor();
    await confirmation.getByText(/Yalnızca bu servisin mevcut rolleri kaldırılacak/).waitFor();
    await confirmation.getByRole('button', { name: 'Tamam', exact: true }).click();
    await page.getByText('Rol atama sonucu', { exact: true }).waitFor();
    assert.deepEqual(writes.at(-1), { path: '/api/users/bulk/coaching/role', method: 'POST', body: { userIds: ['role-fixture'], roleName: 'Student', removeExistingRoles: true } });
    assert.equal(await page.locator('select[name="assignmentRole"] option[value="SystemAdmin"]').count(), 0);
    await page.screenshot({ path: output + '/bulk-' + (mobile ? 'mobile' : 'desktop') + '.png', fullPage: true });
    assert.deepEqual(errors, []);
    results.push({ viewport: mobile ? 'mobile' : 'desktop', checks: ['list service labels', 'service required', 'scoped removal', 'other service preserved', 'scoped addition', '403 preserves roles', 'list refreshed', 'bulk requires service', 'bulk confirmation scope', 'bulk API scope and global exclusion'], writes });
    await context.close();
  }
  await writeFile(output + '/results.json', JSON.stringify({ passed: results.reduce((sum, item) => sum + item.checks.length, 0), results }, null, 2));
  console.log(JSON.stringify({ passed: results.reduce((sum, item) => sum + item.checks.length, 0), output }));
} finally { await browser.close(); }

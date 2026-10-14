import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';

const origin = process.env.SUBSCRIPTION_UI_ORIGIN ?? 'http://127.0.0.1:4198';
assert.ok(['http://127.0.0.1:4198', 'https://eduivme.com'].includes(origin));
const output = origin.startsWith('https:') ? 'artifacts/subscription-requirement-live-ui' : 'artifacts/subscription-requirement-ui';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
try {
  for (const mobile of [false, true]) {
    const page = await browser.newPage({ viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 } });
    const errors = [], writes = []; page.on('pageerror', error => errors.push(error.message));
    let speed = true;
    let coaching = { requireActiveSubscription: false, currency: 'TRY', accountHolder: 'Original', bankName: 'Original bank',
      iban: 'TR330006100519786457841326', paymentInstructions: 'Original instructions', bankTransferEnabled: true,
      isPubliclyAvailable: true, updatedAt: null };
    await page.route('**/api/**', async route => {
      const request = route.request(); const path = new URL(request.url()).pathname;
      // Session and API traffic is intercepted; a fresh browser context has no real credentials.
      let data = [];
      if (path.endsWith('/auth/refresh-token')) {
        const payload = { sub: 'local-ui-fixture', role: 'SystemAdmin', amr: 'mfa',
          permission: ['Permissions.SpeedReading.ContentManage', 'Permissions.Coaching.SubscriptionManage'] };
        return route.fulfill({ json: { accessToken: `e30.${Buffer.from(JSON.stringify(payload)).toString('base64url')}.fixture`, tokenType: 'Bearer', expiresInMinutes: 15 } });
      }
      if (path.endsWith('/speed-reading/subscriptions/settings')) {
        if (request.method() === 'PUT') { writes.push({ path, body: request.postDataJSON() }); speed = request.postDataJSON().requireActiveSubscription; }
        data = { requireActiveSubscription: speed, updatedAt: null };
      } else if (path.endsWith('/coaching-admin/subscriptions/settings')) {
        if (request.method() === 'PUT') { writes.push({ path, body: request.postDataJSON() }); coaching = { ...coaching, ...request.postDataJSON() }; }
        data = coaching;
      } else if (path.includes('/institutions')) data = { items: [], totalCount: 0 };
      else if (request.method() !== 'GET') return route.fulfill({ status: 500, json: { message: 'Unexpected local write' } });
      await route.fulfill({ json: { success: true, data } });
    });
    for (const service of ['speed-reading', 'coaching']) {
      await page.goto(`${origin}/dashboard/${service}/subscriptions`);
      const toggle = page.getByRole('switch'); await toggle.waitFor({ state: 'visible' });
      const save = page.getByRole('button', { name: service === 'speed-reading' ? 'Ayarı kaydet' : 'Ayarları kaydet', exact: true });
      await save.waitFor({ state: 'visible' });
      for (const required of [false, true]) {
        await toggle.setChecked(required); await save.click();
        await page.getByText(`Kaydedilmiş durum: ${required ? 'Açık — abonelik gerekli' : 'Kapalı — abonelik gerekli değil'}`, { exact: true }).waitFor();
      }
      await page.reload(); await toggle.waitFor({ state: 'visible' }); assert.equal(await toggle.isChecked(), true);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
      await page.screenshot({ path: `${output}/${service}-${mobile ? 'mobile' : 'desktop'}.png`, fullPage: true });
      results.push(`${service}-${mobile ? 'mobile' : 'desktop'}-off-on-reload`);
    }
    assert.ok(writes.some(write => write.path.includes('/speed-reading/') && Object.keys(write.body).length === 1));
    assert.ok(writes.filter(write => write.path.includes('/coaching-admin/')).every(write => write.body.bankName === 'Original bank'));
    assert.deepEqual(errors, []); await page.close();
  }
  await writeFile(`${output}/results.json`, JSON.stringify({ passed: results.length, checks: results }, null, 2));
  console.log(JSON.stringify({ passed: results.length, checks: results }));
} finally { await browser.close(); }

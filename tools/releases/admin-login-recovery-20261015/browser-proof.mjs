import { chromium } from '../../../tests/E2E/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const context = await browser.newContext({ serviceWorkers: 'block' });
  await context.route('**/api/**', route => route.fulfill({ status: 400, json: { error: 'Unauthenticated fixture' } }));
  const page = await context.newPage();
  await page.goto('https://eduivme.com/auth/login');
  const modules = await page.evaluate(async () => {
    const results = [];
    for (const path of ['/chunk-U6CFYNLY.js', '/chunk-FDZP6X4B.js']) {
      const module = await import(path);
      results.push({ path, exports: Object.keys(module).length });
    }
    return results;
  });
  assert.ok(modules.every(module => module.exports > 0));
  await mkdir('artifacts/admin-login-recovery-live', { recursive: true });
  await writeFile('artifacts/admin-login-recovery-live/browser-results.json', JSON.stringify({ passed: true, modules, scope: 'old module import, API mocked, no live authentication' }, null, 2));
  console.log(JSON.stringify({ passed: true, modules }));
} finally { await browser.close(); }

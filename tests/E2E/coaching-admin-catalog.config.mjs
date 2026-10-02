import { defineConfig } from '@playwright/test';
if (process.env.E2E_DISPOSABLE_ENV !== 'true') throw new Error('Only disposable local tests are supported.');
export default defineConfig({
  globalSetup: './support/coaching-planning-preflight.mjs',
  testDir: './specs', testMatch: 'coaching-admin-catalog.spec.mjs', workers: 1, retries: 0,
  outputDir: '../../artifacts/local-admin-catalog-e2e/results', timeout: 60_000,
  use: { channel: 'msedge', baseURL: 'http://127.0.0.1:4300', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  reporter: [['list']]
});

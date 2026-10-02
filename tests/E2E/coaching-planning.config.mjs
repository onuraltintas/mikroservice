import { defineConfig } from '@playwright/test';
if (process.env.E2E_DISPOSABLE_ENV !== 'true') throw new Error('Planning tests require a disposable local environment.');
export default defineConfig({
  testDir: './specs', testMatch: 'coaching-study-planning.spec.mjs', workers: 1, retries: 0,
  outputDir: '../../artifacts/local-planning-e2e/results', timeout: 45_000,
  use: { channel: 'msedge', baseURL: 'http://127.0.0.1:4300', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  reporter: [['list']]
});

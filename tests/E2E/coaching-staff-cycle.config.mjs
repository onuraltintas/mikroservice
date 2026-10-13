import { defineConfig } from '@playwright/test';
if (process.env.E2E_DISPOSABLE_ENV !== 'true') throw new Error('Staff cycle tests require disposable local services.');
export default defineConfig({
  globalSetup: './support/coaching-planning-preflight.mjs',
  testDir: './specs', testMatch: 'coaching-staff-cycle.spec.mjs', workers: 1, retries: 0,
  outputDir: '../../artifacts/coaching-completion-20261013/e2e-results', timeout: 90_000,
  use: { channel: 'msedge', baseURL: 'http://127.0.0.1:4300', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  reporter: [['list'], ['json', { outputFile: '../../artifacts/coaching-completion-20261013/e2e-results.json' }]]
});

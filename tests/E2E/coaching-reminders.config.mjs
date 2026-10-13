import { defineConfig } from '@playwright/test';
if (process.env.E2E_DISPOSABLE_ENV !== 'true' || process.env.E2E_COACHING_NOTIFICATIONS !== 'true') throw new Error('Reminder tests require disposable local coaching and notification services.');
export default defineConfig({
  globalSetup: './support/coaching-planning-preflight.mjs',
  testDir: './specs', testMatch: 'coaching-reminders.spec.mjs', workers: 1, retries: 0,
  outputDir: '../../artifacts/coaching-completion-20261013/reminders-e2e-results', timeout: 120_000,
  use: { channel: 'msedge', baseURL: 'http://127.0.0.1:4300', actionTimeout: 15_000, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  reporter: [['list'], ['json', { outputFile: '../../artifacts/coaching-completion-20261013/reminders-e2e-results.json' }]]
});

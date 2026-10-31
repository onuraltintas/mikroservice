import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './specs', testMatch: /coaching-learning-setup\.spec\.mjs/, workers: 1,
  outputDir: '../../artifacts/coaching-learning-setup-tests/browser',
  reporter: [['list'], ['json', { outputFile: '../../artifacts/coaching-learning-setup-tests/browser.json' }]],
  use: { baseURL: 'http://127.0.0.1:4317', channel: 'chrome', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { ...devices['Desktop Chrome'] } }, { name: 'mobile', use: { ...devices['Pixel 7'] } }],
  webServer: { command: 'node support/coaching-learning-ui-server.mjs', url: 'http://127.0.0.1:4317/coaching-portal/account/settings', reuseExistingServer: false }
});

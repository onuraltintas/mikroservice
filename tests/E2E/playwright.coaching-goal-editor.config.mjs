import { defineConfig, devices } from '@playwright/test';
// E7 (#30): Hedefim's goal editor against API fixtures; serves the built admin panel like the learning-setup journeys.
export default defineConfig({
  testDir: './specs', testMatch: /coaching-goal-editor\.spec\.mjs/, workers: 1,
  outputDir: '../../artifacts/coaching-goal-editor/browser',
  reporter: [['list'], ['json', { outputFile: '../../artifacts/coaching-goal-editor/browser.json' }]],
  // PW_CHROMIUM_PATH runs the specs on a bundled Chromium where Google Chrome is not installed.
  use: { baseURL: 'http://127.0.0.1:4317', ...(process.env.PW_CHROMIUM_PATH ? { launchOptions: { executablePath: process.env.PW_CHROMIUM_PATH } } : { channel: 'chrome' }), screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { ...devices['Desktop Chrome'] } }, { name: 'mobile', use: { ...devices['Pixel 7'] } }],
  webServer: { command: 'node support/coaching-learning-ui-server.mjs', url: 'http://127.0.0.1:4317/coaching-portal/growth/goals', reuseExistingServer: false }
});

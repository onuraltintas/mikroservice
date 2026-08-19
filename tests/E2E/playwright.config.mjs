import { defineConfig } from '@playwright/test';

const apiBaseUrl = process.env.E2E_API_BASE_URL ?? process.env.API_BASE_URL ?? 'http://localhost:5000';
const uiBaseUrl = process.env.E2E_UI_BASE_URL ?? process.env.BASE_URL ?? 'http://127.0.0.1:4200';
const hasAdminCredentials = Boolean(process.env.E2E_ADMIN_EMAIL && process.env.E2E_ADMIN_PASSWORD);

if (process.env.E2E_REQUIRED === 'true' && !hasAdminCredentials) {
  throw new Error('E2E_REQUIRED=true requires E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD.');
}

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['junit', { outputFile: 'playwright-results.xml' }]
  ],
  use: {
    baseURL: apiBaseUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 10_000,
    navigationTimeout: 30_000
  },
  projects: [
    {
      name: 'gateway-api',
      testMatch: /gateway-critical\.spec\.mjs/,
      use: {
        trace: 'off',
        screenshot: 'off',
        video: 'off'
      }
    },
    {
      name: 'admin-panel-ui',
      testMatch: /admin-panel\.spec\.mjs/,
      use: {
        baseURL: uiBaseUrl,
        trace: 'off',
        video: 'off',
        screenshot: 'only-on-failure'
      }
    }
  ],
  webServer: process.env.E2E_START_UI === 'true'
    ? {
        command: 'npm start -- --host 127.0.0.1 --port 4200',
        cwd: '../../clients/admin-panel',
        url: uiBaseUrl,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000
      }
    : undefined
});

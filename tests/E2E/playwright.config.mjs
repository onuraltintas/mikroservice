import { defineConfig } from '@playwright/test';

const apiBaseUrl = process.env.E2E_API_BASE_URL ?? process.env.API_BASE_URL ?? 'http://127.0.0.1:5000';
const uiBaseUrl = process.env.E2E_UI_BASE_URL ?? process.env.BASE_URL ?? 'http://127.0.0.1:4200';
const testResultsDir = process.env.E2E_TEST_RESULTS_DIR ?? 'test-results';
const reportDir = process.env.E2E_REPORT_DIR ?? 'playwright-report';
const junitFile = process.env.E2E_JUNIT_FILE ?? 'playwright-results.xml';
const hasAdminCredentials = Boolean(process.env.E2E_ADMIN_EMAIL && process.env.E2E_ADMIN_PASSWORD);
const runsSupportWrite = process.env.E2E_RUN_SUPPORT_WRITE === 'true';
const runsRegistration = process.env.E2E_RUN_REGISTRATION === 'true';
const runsCoaching = process.env.E2E_RUN_COACHING === 'true';

if (process.env.E2E_REQUIRED === 'true' && !hasAdminCredentials) {
  throw new Error('E2E_REQUIRED=true requires E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD.');
}

if (runsSupportWrite && process.env.E2E_DISPOSABLE_ENV !== 'true') {
  throw new Error('E2E_RUN_SUPPORT_WRITE=true requires E2E_DISPOSABLE_ENV=true.');
}

if (runsRegistration && process.env.E2E_DISPOSABLE_ENV !== 'true') {
  throw new Error('E2E_RUN_REGISTRATION=true requires E2E_DISPOSABLE_ENV=true.');
}

if (runsRegistration && !process.env.E2E_MAILCATCHER_API_BASE_URL) {
  throw new Error('E2E_RUN_REGISTRATION=true requires E2E_MAILCATCHER_API_BASE_URL.');
}

if (runsCoaching && process.env.E2E_DISPOSABLE_ENV !== 'true') {
  throw new Error('E2E_RUN_COACHING=true requires E2E_DISPOSABLE_ENV=true.');
}

if (runsCoaching) {
  const coachingCredentialNames = [
    'E2E_COACHING_TEACHER_EMAIL',
    'E2E_COACHING_TEACHER_PASSWORD',
    'E2E_COACHING_STUDENT_EMAIL',
    'E2E_COACHING_STUDENT_PASSWORD'
  ];
  const missing = coachingCredentialNames.filter(name => !process.env[name]);
  if (missing.length > 0) {
    throw new Error(`E2E_RUN_COACHING=true requires ${missing.join(', ')}.`);
  }
}

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  outputDir: testResultsDir,
  reporter: [
    ['list'],
    ['html', { outputFolder: reportDir, open: 'never' }],
    ['junit', { outputFile: junitFile }]
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
        screenshot: 'off'
      }
    },
    {
      name: 'registration-disposable',
      testMatch: /registration-confirmation\.spec\.mjs/,
      use: {
        baseURL: apiBaseUrl,
        trace: 'off',
        screenshot: 'off',
        video: 'off'
      }
    },
    {
      name: 'coaching-disposable',
      testMatch: /coaching-critical\.spec\.mjs/,
      use: {
        baseURL: apiBaseUrl,
        trace: 'off',
        screenshot: 'off',
        video: 'off'
      }
    },
    {
      name: 'speed-reading-assessment-api',
      testMatch: /speed-reading-assessment\.spec\.mjs/,
      use: {
        baseURL: apiBaseUrl,
        trace: 'off',
        screenshot: 'off',
        video: 'off'
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

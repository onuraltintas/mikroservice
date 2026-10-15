import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-auto-scroll-ui.spec.mjs', workers: 1, timeout: 60000,
  outputDir: '../../artifacts/reading-auto-scroll-20261015/e2e-results',
  use: { baseURL: process.env.READING_SCROLL_UI_BASE_URL || 'http://127.0.0.1:4350',
    channel: 'msedge', serviceWorkers: 'block', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } },
    { name: 'short-screen', use: { viewport: { width: 1280, height: 600 } } }
  ],
  reporter: [['list']]
});

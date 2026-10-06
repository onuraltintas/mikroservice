import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-comprehension-ui.spec.mjs',
  workers: 1, retries: 0, timeout: 30_000,
  outputDir: '../../artifacts/comprehension-ui/results',
  use: { baseURL: 'http://127.0.0.1:4310', channel: 'msedge', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } }
  ],
  reporter: [['list']],
  webServer: {
    command: 'npm start -- --host 127.0.0.1 --port 4310', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4310', reuseExistingServer: true, timeout: 120_000
  }
});

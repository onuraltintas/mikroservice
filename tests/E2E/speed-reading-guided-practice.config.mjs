import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-guided-practice-ui.spec.mjs', workers: 1, timeout: 45000,
  outputDir: '../../artifacts/guided-reading-ui/results',
  use: { baseURL: 'http://127.0.0.1:4337', channel: 'msedge', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } }],
  webServer: { command: 'npm start -- --host 127.0.0.1 --port 4337', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4337', reuseExistingServer: false, timeout: 120000 }
});

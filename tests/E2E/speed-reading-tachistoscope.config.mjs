import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-tachistoscope-ui.spec.mjs', workers: 1,
  use: { baseURL: 'http://127.0.0.1:4318', channel: 'msedge' },
  projects: [{ name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } }],
  webServer: { command: 'npm start -- --host 127.0.0.1 --port 4318', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4318', reuseExistingServer: true, timeout: 120000 }
});

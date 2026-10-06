import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-visualization-ui.spec.mjs', workers: 1, timeout: 60000,
  outputDir: '../../artifacts/visualization-ui/results',
  use: { baseURL: 'http://127.0.0.1:4315', channel: 'msedge', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } }],
  webServer: { command: 'npm start -- --host 127.0.0.1 --port 4315', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4315', reuseExistingServer: true, timeout: 120000 }
});

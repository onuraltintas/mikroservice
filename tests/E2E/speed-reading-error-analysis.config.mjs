import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './specs', testMatch: 'speed-reading-error-analysis-ui.spec.mjs', workers: 1, timeout: 40000,
  outputDir: '../../artifacts/error-analysis-ui/results',
  use: { baseURL: 'http://127.0.0.1:4319', channel: 'msedge', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium' } }],
  webServer: { command: 'node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4319', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4319', reuseExistingServer: true, timeout: 120000 }
});

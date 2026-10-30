import { devices } from '@playwright/test';
const exe = { launchOptions: { executablePath: '/opt/pw-browsers/chromium-1243/chrome-linux64/chrome' } };
export default { testDir: './specs', testMatch: 'speed-reading-target-hunt-ui.spec.mjs', workers: 1, timeout: 90000,
  outputDir: '/tmp/claude-0/th-results',
  use: { baseURL: 'http://127.0.0.1:4320', ...exe },
  projects: [{ name: 'desktop', use: { viewport: { width: 1440, height: 1000 }, ...exe } },
    { name: 'mobile', use: { ...devices['Pixel 5'], defaultBrowserType: 'chromium', ...exe } }],
  webServer: { command: 'node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4320', cwd: '../../clients/speed-reading',
    url: 'http://127.0.0.1:4320', reuseExistingServer: true, timeout: 240000 } };

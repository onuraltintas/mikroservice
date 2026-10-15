import { defineConfig } from '@playwright/test';
import grouping from './speed-reading-grouping.config.mjs';
export default defineConfig({ ...grouping, testMatch: 'speed-reading-day-advance.spec.mjs', webServer: undefined,
  outputDir: '../../artifacts/day-advance-20261015/e2e-results',
  use: { ...grouping.use, baseURL: 'http://127.0.0.1:4352', serviceWorkers: 'block' }
});

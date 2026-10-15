import { defineConfig } from '@playwright/test';
import grouping from './speed-reading-grouping.config.mjs';

export default defineConfig({ ...grouping, webServer: undefined,
  outputDir: '../../artifacts/reading-content-progress-20261015/e2e-results',
  use: { ...grouping.use, baseURL: process.env.READING_PROGRESS_UI_BASE_URL || 'http://127.0.0.1:4352', serviceWorkers: 'block' }
});

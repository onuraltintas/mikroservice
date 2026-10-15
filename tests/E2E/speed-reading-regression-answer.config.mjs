import { defineConfig } from '@playwright/test';
import regression from './speed-reading-regression.config.mjs';

export default defineConfig({ ...regression, webServer: undefined,
  outputDir: '../../artifacts/regression-answer-label-20261015/e2e-results',
  use: { ...regression.use, baseURL: 'http://127.0.0.1:4352', serviceWorkers: 'block' }
});

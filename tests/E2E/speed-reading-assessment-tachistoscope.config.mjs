import { defineConfig } from '@playwright/test';
import base from './speed-reading-payment.config.mjs';

export default defineConfig({ ...base, testMatch: 'speed-reading-assessment-tachistoscope-ui.spec.mjs',
  use: { ...base.use, baseURL: process.env.ASSESSMENT_UI_BASE_URL || base.use.baseURL, serviceWorkers: 'block' },
  webServer: process.env.ASSESSMENT_UI_BASE_URL ? undefined : base.webServer,
  outputDir: '../../artifacts/assessment-tachistoscope-ui/results' });

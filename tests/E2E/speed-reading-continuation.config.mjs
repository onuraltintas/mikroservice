import { defineConfig } from '@playwright/test';
import base from './speed-reading-payment.config.mjs';

export default defineConfig({ ...base, testMatch: 'speed-reading-continuation-ui.spec.mjs',
  outputDir: '../../artifacts/continuation-ui/results' });

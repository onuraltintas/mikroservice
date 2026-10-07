import { defineConfig } from '@playwright/test';
import base from './speed-reading-skimming.config.mjs';
export default defineConfig({ ...base, testMatch: 'speed-reading-tracking-ui.spec.mjs',
  outputDir: '../../artifacts/tracking-ui/results' });

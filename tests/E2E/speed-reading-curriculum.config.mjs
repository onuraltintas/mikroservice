import { defineConfig } from '@playwright/test';
import base from './speed-reading-skimming.config.mjs';
export default defineConfig({ ...base, testMatch: 'speed-reading-curriculum-ui.spec.mjs',
  outputDir: '../../artifacts/curriculum-v3-ui/results' });

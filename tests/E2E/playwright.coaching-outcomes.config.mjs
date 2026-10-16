import base from './playwright.coaching-learning.config.mjs';
export default { ...base, testMatch: /coaching-outcomes\.spec\.mjs/,
  outputDir: '../../artifacts/coaching-outcomes/browser',
  reporter: [['list'], ['json', { outputFile: '../../artifacts/coaching-outcomes/browser.json' }]] };

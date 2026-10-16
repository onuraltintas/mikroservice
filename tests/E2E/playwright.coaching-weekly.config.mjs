import base from './playwright.coaching-learning.config.mjs';
export default {...base,testMatch:/coaching-weekly\.spec\.mjs/,outputDir:'../../artifacts/coaching-weekly/browser',reporter:[['list'],['json',{outputFile:'../../artifacts/coaching-weekly/browser.json'}]]};

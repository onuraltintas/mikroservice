import base from './playwright.coaching-learning.config.mjs';
export default {...base,testMatch:/coaching-curriculum\.spec\.mjs/,outputDir:'../../artifacts/coaching-curriculum/browser',reporter:[['list'],['json',{outputFile:'../../artifacts/coaching-curriculum/browser.json'}]]};

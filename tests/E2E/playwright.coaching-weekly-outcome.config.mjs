import base from './playwright.coaching-weekly.config.mjs';
export default {...base,testMatch:/coaching-weekly(?:-outcome)?\.spec\.mjs/,outputDir:'../../artifacts/weekly-outcome-browser',reporter:[['list'],['json',{outputFile:'../../artifacts/weekly-outcome-browser.json'}]]};

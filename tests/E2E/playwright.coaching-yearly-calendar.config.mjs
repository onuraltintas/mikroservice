import base from './playwright.coaching-weekly-outcome.config.mjs';
export default {...base,testMatch:/coaching-(?:weekly(?:-outcome)?|yearly-calendar)\.spec\.mjs/,outputDir:'../../artifacts/yearly-calendar-browser',reporter:[['list'],['json',{outputFile:'../../artifacts/yearly-calendar-browser.json'}]]};

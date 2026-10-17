import base from './playwright.coaching-calendar-move.config.mjs';
export default {...base,testMatch:/coaching-(?:weekly(?:-outcome)?|yearly-calendar|calendar-move|restore-profile|assignment-follow-up)\.spec\.mjs/,outputDir:'../../artifacts/coaching-followup-browser',reporter:[['list'],['json',{outputFile:'../../artifacts/coaching-followup-browser.json'}]]};

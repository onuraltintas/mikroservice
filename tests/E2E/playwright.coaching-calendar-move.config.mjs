import base from './playwright.coaching-yearly-calendar.config.mjs';
export default {...base,testMatch:/coaching-(?:weekly(?:-outcome)?|yearly-calendar|calendar-move)\.spec\.mjs/,outputDir:'../../artifacts/calendar-move-browser',reporter:[['list'],['json',{outputFile:'../../artifacts/calendar-move-browser.json'}]],webServer:{...base.webServer,command:'node support/calendar-move-ui-server.mjs'}};

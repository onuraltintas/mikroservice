import base from './playwright.coaching-yearly-calendar.config.mjs';
export default {...base,outputDir:'../../artifacts/coaching-yearly-calendar-live-20261017/browser',reporter:[['list'],['json',{outputFile:'../../artifacts/coaching-yearly-calendar-live-20261017/browser.json'}]],webServer:{...base.webServer,command:'node ../../tools/releases/coaching-yearly-calendar-20261017/ui-server.mjs'}};

import base from './playwright.coaching-weekly-outcome.config.mjs';
export default {...base,outputDir:'../../artifacts/coaching-weekly-outcomes-live-20261017/browser',reporter:[['list'],['json',{outputFile:'../../artifacts/coaching-weekly-outcomes-live-20261017/browser.json'}]],webServer:{...base.webServer,command:'node ../../tools/releases/coaching-weekly-outcomes-20261017/ui-server.mjs'}};

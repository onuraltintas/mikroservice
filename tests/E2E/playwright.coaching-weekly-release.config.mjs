import base from './playwright.coaching-weekly.config.mjs';
export default {...base,webServer:{...base.webServer,command:'node ../../tools/releases/coaching-weekly-calendar-20261017/ui-server.mjs'}};

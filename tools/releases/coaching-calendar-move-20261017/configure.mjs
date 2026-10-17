import {readFileSync,writeFileSync} from 'node:fs';
const here='tools/releases/coaching-calendar-move-20261017/',artifact='artifacts/coaching-calendar-move-live-20261017/';
let deploy=readFileSync('tools/releases/coaching-weekly-outcomes-20261017-r2/deploy.sh','utf8').replaceAll('coaching-weekly-outcomes-20261017-r2','coaching-calendar-move-20261017').replaceAll('coaching_weekly_outcomes_drill_20261017_r2','coaching_calendar_move_drill_20261017');
deploy=deploy.replace('services=(coaching-service admin-panel)','services=(coaching-service admin-panel staff-portal)').replace('eduivme-production-admin-panel-1) continue','eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue');
deploy=deploy.replace("  for attempt in $(seq 1 40); do","  [[ \"$service\" != staff-portal ]] || { port=80; path=/; }\n  for attempt in $(seq 1 40); do");
deploy=deploy.replace('20261017061920_WeeklyOutcomePlans','20261017140000_ManualTimedStudyTasks').replace('Weekly outcome migration missing','Manual timed migration missing');
deploy=deploy.replace('sha256:9af75dc5ff69f8b2b0b417355b6d36a96d3e7fecc9ddfb2744985f1d5644287b','sha256:4fac5d3cadbd2d45fbdfefe61fb3699001fbb70d1a1d916903ad100ffef434f2').replace('sha256:76587e593fe77563f1d7e52dda46c4cd60ac28603504ef25e8901a0170ae0c74','sha256:35f4b6c548ec9499b3f879d78bd5d4eb075eee8da7a8b989aebe6b4f6dd89894');
deploy=deploy.replace('-f /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017/production.compose.json','-f /var/lib/eduivme/releases/coaching-yearly-calendar-20261017/production.compose.json');
deploy=deploy.replace('    "${compose[@]}" -f /var/lib','    [[ $(docker inspect eduivme-production-staff-portal-1 --format \'{{.Image}}\') == sha256:325cfe33c6533cd0bbb987f59c2e88a88f919a4bbfee55bb7919c4a47206b8b2 ]]\n    "${compose[@]}" -f /var/lib');
deploy=deploy.replace('.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)', '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)');
deploy=deploy.replace('docker stop --time 30 eduivme-production-admin-panel-1 eduivme-production-coaching-service-1','docker stop --time 30 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 eduivme-production-coaching-service-1').replaceAll('two services','three services').replaceAll('two images','three images');
// Stable baseline-row identity lets post-browser verification reject any lost or unexpectedly changed original work.
deploy=deploy.replace("'SELECT %L,md5((to_jsonb(t)-%L::text[])::text) FROM %I.%I t;'","'SELECT %L,coalesce(to_jsonb(t)->>''Id'',md5(to_jsonb(t)::text)),md5((to_jsonb(t)-%L::text[])::text) FROM %I.%I t;'");
writeFileSync(here+'deploy.sh',deploy.replaceAll('\r\n','\n'));
let pack=readFileSync('tools/releases/coaching-weekly-outcomes-20261017-r2/package.mjs','utf8').replaceAll('coaching-weekly-outcomes-live-20261017-r2','coaching-calendar-move-live-20261017').replaceAll('coaching-weekly-outcomes-20261017-r2','coaching-calendar-move-20261017').replace("['coaching-service','admin-panel']","['coaching-service','admin-panel','staff-portal']");
pack=pack.replace("for(const service of", "cpSync(resolve(release,'staff-source/clients/admin-panel/dist/staff-portal/browser'),resolve(pack,'payload/staff'),{recursive:true});\nfor(const service of");
writeFileSync(here+'package.mjs',pack);
for(const file of ['stage.sh','start-monitor.sh','monitor.sh']){
 let text=readFileSync('tools/releases/coaching-yearly-calendar-20261017/'+file,'utf8').replaceAll('coaching-yearly-calendar-20261017','coaching-calendar-move-20261017').replace('for service in coaching-service admin-panel;','for service in coaching-service admin-panel staff-portal;').replace('for url in https://eduivme.com/auth/login','for url in https://eduivme.com/staff/ https://eduivme.com/auth/login');
 writeFileSync(here+file,text.replaceAll('\r\n','\n'));
}
let server=readFileSync('tests/E2E/support/calendar-move-ui-server.mjs','utf8').replace('new URL(`../../../clients/admin-panel/dist/${app}/browser/`,import.meta.url)','new URL(`../../${app===\'admin-panel\'?\'admin-source\':\'staff-source\'}/clients/admin-panel/dist/${app}/browser/`,import.meta.url)');
server=server.replace('`../../${app===','`../${app===');
writeFileSync(artifact+'ui-server.mjs',server.replace('`../${app===','`./${app==='));
writeFileSync(artifact+'playwright.config.mjs',`import base from '../../tests/E2E/playwright.coaching-calendar-move.config.mjs';\nexport default {...base,testDir:'../../tests/E2E',outputDir:'./browser-results',reporter:[['list']],webServer:{...base.webServer,command:'node ../../${artifact}ui-server.mjs'}};\n`);

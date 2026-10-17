import {cpSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const old='tools/releases/coaching-weekly-outcomes-20261017',dir=old+'-r2';mkdirSync(dir,{recursive:true});
const tag='coaching-weekly-outcomes-20261017-r2';
for(const file of ['prepare.mjs','package.mjs','deploy.sh','stage.sh','monitor.sh','monitor-status.sh','start-monitor.sh','status.sh','diagnostics.sh','read-results.sh','verify-preserved.sh','inspect-versions.sh']){
  let text=readFileSync(old+'/'+file,'utf8').replaceAll('coaching-weekly-outcomes-live-20261017','coaching-weekly-outcomes-live-20261017-r2').replaceAll('coaching-weekly-outcomes-20261017',tag).replaceAll('coaching_weekly_outcomes_drill_20261017','coaching_weekly_outcomes_drill_20261017_r2');
  if(file==='prepare.mjs')text=text.replace("artifacts/coaching-weekly-calendar-live-20261017","artifacts/coaching-weekly-outcomes-live-20261017").replace("base='12951c5a'","base='119fc711'").replace('files.length>20','files.length===1').replace("previousRelease:'coaching-weekly-calendar-20261017'","previousRelease:'coaching-weekly-outcomes-20261017'");
  if(file==='deploy.sh'){
    text=text.replaceAll('sha256:6d39d2d0e881100d581196fc655b6a7a31e78546845210bb5a194cd6d0983421','sha256:9af75dc5ff69f8b2b0b417355b6d36a96d3e7fecc9ddfb2744985f1d5644287b').replaceAll('sha256:efd0ead5bf0455f077b103d992a382f3365c16e78c141f9911a6bbe1fb6b989a','sha256:76587e593fe77563f1d7e52dda46c4cd60ac28603504ef25e8901a0170ae0c74').replace('/var/lib/eduivme/releases/coaching-weekly-calendar-20261017/production.compose.json','/var/lib/eduivme/releases/coaching-weekly-outcomes-20261017/production.compose.json');
    text=text.replaceAll("CASE table_name WHEN 'study_plan_revisions' THEN '{WeeklySource}' WHEN 'study_plan_tasks' THEN '{PlannedStartMinute,WeeklyLessonId,LessonNameSnapshot}' ELSE '{}' END","'{}'");
  }
  if(file==='deploy.sh')assert.ok(text.includes(tag));writeFileSync(dir+'/'+file,text);
}
// The student UI is byte-for-byte identical; reuse its already verified production bundle.
cpSync('artifacts/coaching-weekly-outcomes-live-20261017/admin-source/clients/admin-panel/dist','artifacts/coaching-weekly-outcomes-live-20261017-r2/admin-source/clients/admin-panel/dist',{recursive:true});

import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const dir='tools/releases/coaching-weekly-outcomes-20261017',old='tools/releases/coaching-weekly-calendar-20261017';
const tag='coaching-weekly-outcomes-20261017';
let deploy=readFileSync(old+'/deploy.sh','utf8').replaceAll('\r\n','\n');
const replace=(from,to)=>{assert.ok(deploy.includes(from),from);deploy=deploy.replaceAll(from,to);};
replace('coaching-weekly-calendar-20261017',tag);
replace('coaching_weekly_calendar_drill_20261017','coaching_weekly_outcomes_drill_20261017');
replace('sha256:0537fb8da9ef576ba5d56ae0e2061e1c7002fd524bda0959468e5c0b278b1fa0','sha256:6d39d2d0e881100d581196fc655b6a7a31e78546845210bb5a194cd6d0983421');
replace('sha256:bed0fd6a2a66a82cf6d267c3b3a227967e141e52004c217ebf5c13f69c849a4d','sha256:efd0ead5bf0455f077b103d992a382f3365c16e78c141f9911a6bbe1fb6b989a');
replace('/var/lib/eduivme/releases/coaching-exam-date-20261016/production.compose.json','/var/lib/eduivme/releases/coaching-weekly-calendar-20261017/production.compose.json');
replace(",'study_weekly_drafts')",")");
const exclusion=`CASE table_name WHEN 'study_plan_revisions' THEN '{WeeklySource}' WHEN 'study_plan_tasks' THEN '{PlannedStartMinute,WeeklyLessonId,LessonNameSnapshot}' ELSE '{}' END`;
replace("'{}',\n'','{}',",exclusion+",\n'',"+exclusion+",");
replace("20261016213358_WeeklyStudyDraft","20261017061920_WeeklyOutcomePlans");
replace('Weekly draft migration missing','Weekly outcome migration missing');
replace('verifyDb() {',`fingerprintRows() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,md5((to_jsonb(t)-%L::text[])::text) FROM %I.%I t;',
table_schema||'.'||table_name,${exclusion},table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_weekly_drafts') ORDER BY table_name
\\gexec
SQL
}
verifyDb() {`);
replace('accounts > accounts.live.before.txt; fingerprint coaching_db > coaching.live.before.txt','accounts > accounts.live.before.txt; fingerprint coaching_db > coaching.live.before.txt; fingerprintRows > coaching.rows.before.txt');
replace("IF EXISTS(SELECT 1 FROM coaching.study_weekly_drafts GROUP BY",`IF (SELECT count(*) FROM information_schema.columns WHERE table_schema='coaching' AND ((table_name='study_plan_revisions' AND column_name='WeeklySource') OR (table_name='study_plan_tasks' AND column_name IN ('PlannedStartMinute','WeeklyLessonId','LessonNameSnapshot'))))<>4 THEN RAISE EXCEPTION 'Expected four additive columns'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_weekly_drafts GROUP BY`);
writeFileSync(dir+'/deploy.sh',deploy);
for(const file of ['stage.sh','monitor.sh','read-results.sh','start-monitor.sh','monitor-status.sh','diagnostics.sh','status.sh'])
  writeFileSync(dir+'/'+file,readFileSync(old+'/'+file,'utf8').replaceAll('coaching-weekly-calendar-20261017',tag).replaceAll('\r\n','\n'));
let diagnostics=readFileSync(dir+'/diagnostics.sh','utf8').replace('/api/coaching/catalog/exams;','/api/coaching/catalog/exams /api/coaching/study-planning/weekly-outcome-plans/options;');
writeFileSync(dir+'/diagnostics.sh',diagnostics);
writeFileSync(dir+'/ui-server.mjs',readFileSync(old+'/ui-server.mjs','utf8').replaceAll('coaching-weekly-calendar-live-20261017','coaching-weekly-outcomes-live-20261017'));
writeFileSync(dir+'/package.mjs',readFileSync(old+'/package.mjs','utf8').replaceAll('coaching-weekly-calendar-live-20261017','coaching-weekly-outcomes-live-20261017').replaceAll('coaching-weekly-calendar-20261017',tag).replace("execFileSync('git',['rev-parse','12951c5a'],{encoding:'utf8'})","JSON.parse(readFileSync(resolve(release,'source-provenance.json'))).revision+'\\n'"));

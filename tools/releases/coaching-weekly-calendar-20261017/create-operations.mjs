import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const dir='tools/releases/coaching-weekly-calendar-20261017';
let deploy=readFileSync('tools/releases/coaching-exam-date-20261016/deploy.sh','utf8').replaceAll('\r\n','\n');
const replace=(from,to)=>{assert.ok(deploy.includes(from),from);deploy=deploy.replaceAll(from,to);};
replace('coaching-exam-date-20261016','coaching-weekly-calendar-20261017');
replace('coaching_exam_date_drill_20261016','coaching_weekly_calendar_drill_20261017');
replace('sha256:a93ac4d5ad1648f82bb6dbc904ec37b26707a25c0c00d4d4e54fb09d9c4071ac','sha256:0537fb8da9ef576ba5d56ae0e2061e1c7002fd524bda0959468e5c0b278b1fa0');
replace('sha256:5602c560a88253a6f120d86d6c6e6806e4f8b63b947d2ed62aadc87a7401be5e','sha256:bed0fd6a2a66a82cf6d267c3b3a227967e141e52004c217ebf5c13f69c849a4d');
replace('/var/lib/eduivme/releases/coaching-curriculum-20261016/production.compose.json','/var/lib/eduivme/releases/coaching-exam-date-20261016/production.compose.json');
replace("CASE table_name WHEN 'study_catalog_exams' THEN '{ExamDate}' ELSE '{}' END","'{}'");
replace("'__ef_migrations_history','__EFMigrationsHistory')","'__ef_migrations_history','__EFMigrationsHistory','study_weekly_drafts')");
const start=deploy.indexOf('verifyDb() {'),end=deploy.indexOf('case "${1:-validate}" in',start);assert.ok(start>0&&end>start);
deploy=deploy.slice(0,start)+`verifyDb() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM coaching.__ef_migrations_history WHERE "MigrationId"='20261016213358_WeeklyStudyDraft') THEN RAISE EXCEPTION 'Weekly draft migration missing'; END IF;
  IF to_regclass('coaching.study_weekly_drafts') IS NULL THEN RAISE EXCEPTION 'Weekly draft table missing'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_weekly_drafts GROUP BY "StudentId" HAVING count(*)>1) THEN RAISE EXCEPTION 'Duplicate student draft'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes t LEFT JOIN coaching.study_plan_tasks p ON p."Id"=t."TaskId" WHERE p."Id" IS NULL) THEN RAISE EXCEPTION 'Orphaned plan outcome'; END IF;
END $$;
SELECT json_build_object('passed',true,'plans',(SELECT count(*) FROM coaching.study_plan_revisions),
'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),
'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),
'drafts',(SELECT count(*) FROM coaching.study_weekly_drafts),'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SQL
}
`+deploy.slice(end);
writeFileSync(dir+'/deploy.sh',deploy);
for(const file of ['stage.sh','monitor.sh','read-results.sh'])writeFileSync(dir+'/'+file,readFileSync('tools/releases/coaching-exam-date-20261016/'+file,'utf8').replaceAll('coaching-exam-date-20261016','coaching-weekly-calendar-20261017').replaceAll('/dashboard/coaching/curriculum','/coaching-portal/weekly-plan').replaceAll('\r\n','\n'));
writeFileSync(dir+'/package.mjs',readFileSync('tools/releases/coaching-exam-date-20261016/package.mjs','utf8').replaceAll('coaching-exam-date-live-20261016','coaching-weekly-calendar-live-20261017').replaceAll('coaching-exam-date-20261016','coaching-weekly-calendar-20261017').replaceAll('dfbb4335','12951c5a'));

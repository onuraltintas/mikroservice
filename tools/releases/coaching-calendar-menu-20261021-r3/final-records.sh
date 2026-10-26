set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
bash protected-records.sh verify
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcomes',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'active plan',"Id","Version" FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3';
SELECT 'new completed',"Id","Title","IsCompleted","ActualMinutes","ActualQuestions","PlannedDate","PlannedStartMinute" FROM coaching.study_plan_tasks WHERE "Title"='CANLI TAKVİM 21.10 · öğrenci düzenlendi';
SELECT 'pending test rows',count(*) FROM coaching.study_plan_tasks WHERE "Title" IN ('CANLI TAKVİM 21.10 · silme kontrolü','CANLI TAKVİM 21.10 · öğretmen düzenlendi');
SELECT 'protected completed',"Id","IsCompleted","ActualMinutes" FROM coaching.study_plan_tasks WHERE "Id" IN ('294578cc-e8e0-431d-a25b-a0a0d1674ca5','6f8117eb-59fd-4268-b69e-53a9d804025d');
COMMIT;
SQL
started=$(<promotion-start.txt)
docker logs --since "$started" eduivme-production-coaching-service-1 > api-observation.log 2>&1
if grep -E '\bERR\]|\bFTL\]|Unhandled exception' api-observation.log > api-errors.txt; then cat api-errors.txt; exit 1; fi
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'; done
printf 'API error count: '; wc -l < api-errors.txt

set -euo pipefail
cd /var/lib/eduivme/releases/coaching-daily-agenda-20261021
bash history.sh after
bash history.sh compare
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcomes',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'active plan',"Id","Version" FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3';
SELECT 'new daily task',"Id","Title","PlannedDate","IsCompleted","ActualMinutes" FROM coaching.study_plan_tasks WHERE "Title" LIKE 'CANLI GÜNLÜK 21.10%';
SELECT 'new daily decision',"Id","Title","Status" FROM coaching.weekly_decisions WHERE "Title" LIKE 'CANLI GÜNLÜK 21.10%';
SELECT 'new daily assignment',a.id,a.title,s.status,s.score,s.submitted_at IS NOT NULL FROM coaching.assignments a JOIN coaching.assignment_students s ON s.assignment_id=a.id WHERE a.title LIKE 'CANLI GÜNLÜK 21.10%';
COMMIT;
SQL
started=$(<promotion-start.txt)
docker logs --since "$started" eduivme-production-coaching-service-1 > api-observation.log 2>&1
if grep -E '\bERR\]|\bFTL\]|Unhandled exception' api-observation.log > api-errors.txt; then cat api-errors.txt; exit 1; fi
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'; done
printf 'API errors: '; wc -l < api-errors.txt
printf 'Observation since: '; cat promotion-start.txt
date -u +%FT%TZ

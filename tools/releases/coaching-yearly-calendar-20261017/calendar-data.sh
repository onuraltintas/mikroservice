set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('planId',"Id",'status',"Status",'version',"Version",'source',"WeeklySource") FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SELECT json_build_object('tasks',count(*),'plannedMinutes',sum("PlannedMinutes"),'firstDate',min("PlannedDate"),'lastDate',max("PlannedDate"),'completed',count(*) FILTER(WHERE "IsCompleted")) FROM coaching.study_plan_tasks WHERE "RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SELECT 'unique-outcomes',count(DISTINCT o."OutcomeId"),sum(o."AllocatedMinutes") FROM coaching.study_plan_task_outcomes o JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SQL

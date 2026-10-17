set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('planId',p."Id",'status',p."Status",'studentId',p."StudentId",'createdBy',p."CreatedBy",'taskId',t."Id",'date',t."PlannedDate",'startMinute',t."PlannedStartMinute",'minutes',t."PlannedMinutes",'completed',t."IsCompleted") FROM coaching.study_plan_revisions p JOIN coaching.study_plan_tasks t ON t."RevisionId"=p."Id" WHERE p."Id"='b5536157-dd00-4f63-b522-9f250c77b33b';
SELECT 'annual',"Status","IsActive","Version" FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SQL

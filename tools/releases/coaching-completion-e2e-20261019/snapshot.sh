set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'PLAN|'||md5(to_jsonb(p)::text) FROM coaching.study_plan_revisions p WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3';
SELECT 'TASK|'||"Id"||'|'||md5(to_jsonb(t)::text)||'|'||md5((to_jsonb(t)-'{ActualMinutes,ActualQuestions,IsCompleted,CompletedAt,UpdatedAt,UpdatedBy,Version}'::text[])::text) FROM coaching.study_plan_tasks t ORDER BY "Id";
SELECT 'LINK|'||md5(to_jsonb(l)::text) FROM coaching.study_plan_task_outcomes l ORDER BY "TaskId","OutcomeId";
SELECT 'OTHER|'||table_name||'|'||count(*) FROM information_schema.columns WHERE table_schema='coaching' GROUP BY table_name ORDER BY table_name;
SELECT json_build_object('id',t."Id",'title',t."Title",'date',t."PlannedDate",'start',t."PlannedStartMinute",'planned',t."PlannedMinutes",'completed',t."IsCompleted",'actual',t."ActualMinutes",'questions',t."ActualQuestions",'topic',t."TopicId",'version',t."Version") FROM coaching.study_plan_tasks t WHERE "RevisionId"='a4cea155-5485-4db6-9f52-1ec4df8080c3' AND "PlannedDate"='2026-10-19' ORDER BY "PlannedStartMinute";
SELECT json_build_object('plans',count(*),'active',count(*) FILTER(WHERE "IsActive")) FROM coaching.study_plan_revisions;
SELECT json_build_object('tasks',count(*),'completed',count(*) FILTER(WHERE "IsCompleted"),'actual',coalesce(sum("ActualMinutes"),0),'planned',sum("PlannedMinutes")) FROM coaching.study_plan_tasks;
SQL

set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'tasks_by_day',"PlannedDate",count(*) FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions p ON p."Id"=t."RevisionId" WHERE p."IsActive" AND NOT t."IsCompleted" AND t."PlannedDate" <= CURRENT_DATE GROUP BY t."PlannedDate" ORDER BY t."PlannedDate" DESC LIMIT 12;
SELECT 'today_work',t."Id",t."Title",t."PlannedDate",t."PlannedStartMinute",t."PlannedMinutes" FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions p ON p."Id"=t."RevisionId" WHERE p."IsActive" AND NOT t."IsCompleted" AND t."PlannedDate" <= CURRENT_DATE ORDER BY t."PlannedDate" LIMIT 5;
SELECT 'assignment_status',status,count(*) FROM coaching.assignments GROUP BY status;
SELECT 'test_assignments',a.id,a.title,a.due_date,s.status,s.submitted_at,s.score FROM coaching.assignments a JOIN coaching.assignment_students s ON s.assignment_id=a.id WHERE a.teacher_id='b184504c-a3fa-4f7d-ad40-71fe418eaa10' AND s.student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' ORDER BY a.created_at DESC LIMIT 8;
COMMIT;
SQL

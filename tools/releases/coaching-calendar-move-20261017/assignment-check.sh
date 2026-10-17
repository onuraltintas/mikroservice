set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'active-assigned-teachers',count(DISTINCT "TeacherUserId") FROM coaching.teacher_student_assignments WHERE "IsActive";
SELECT 'annual-plan-owner-assignments',count(*) FROM coaching.teacher_student_assignments a JOIN coaching.study_plan_revisions p ON p."StudentId"=a."StudentUserId" WHERE p."Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND a."IsActive";
SELECT "Id","PlannedDate","PlannedStartMinute","PlannedMinutes","Version" FROM coaching.study_plan_tasks WHERE "RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "PlannedDate"='2026-10-20' ORDER BY "PlannedStartMinute";
SQL

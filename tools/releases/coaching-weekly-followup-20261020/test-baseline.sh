set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'reflection_week',"WeekStart","Version" FROM coaching.weekly_reflections WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed';
SELECT 'decision',"ReviewWeekStart","DueOn","Version","Status" FROM coaching.weekly_decisions WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed';
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
COMMIT;
SQL

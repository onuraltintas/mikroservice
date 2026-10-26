set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'active_work',count(*),min(t."PlannedDate"),max(t."PlannedDate"),count(*) FILTER(WHERE t."IsCompleted")
FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions p ON p."Id"=t."RevisionId" WHERE p."IsActive";
SELECT 'hours',"TimeZoneId","WindowsJson" FROM coaching.study_availability WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed';
SELECT 'active_slots',t."PlannedDate",t."PlannedStartMinute",t."PlannedMinutes",t."LessonNameSnapshot"
FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions p ON p."Id"=t."RevisionId" WHERE p."IsActive" AND t."PlannedDate" BETWEEN '2026-10-21' AND '2026-10-27' ORDER BY 2,3;
COMMIT;
SQL

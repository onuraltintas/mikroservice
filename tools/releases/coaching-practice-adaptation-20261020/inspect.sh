set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'migration',"MigrationId" FROM coaching.__ef_migrations_history ORDER BY 2 DESC LIMIT 3;
SELECT 'legacy_results',count(*) FROM coaching.exam_results r JOIN coaching.exams e ON r.exam_id=e.id WHERE e.exam_type IN ('LGS','YKS');
SELECT 'student',"UserId" FROM coaching.student_profiles WHERE "UserId" IN (SELECT "StudentId" FROM coaching.study_plan_revisions WHERE "IsActive");
SELECT 'active_plan',"Id","StudentId","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
COMMIT;
SQL
docker inspect eduivme-production-coaching-service-1 --format '{{.Config.Image}} {{.Image}}'
docker inspect eduivme-production-admin-panel-1 --format '{{.Config.Image}} {{.Image}}'
docker inspect eduivme-production-staff-portal-1 --format '{{.Config.Image}} {{.Image}}'
test -d /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/node_modules && echo 'linux_dependency_cache_available'
cat /var/lib/eduivme/releases/unified-target-catalog-20261020-build/build-portals.cjs

set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-tracking-20261020
test -s deployed.txt; test -s fixture-cleaned.txt
bash history.sh after; bash history.sh compare | tee history-verified.log
bash preservation.sh after
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-counts.txt
BEGIN READ ONLY;
SELECT 'active_plan',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'original_submission',assignment_id,score,status FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
SELECT 'test_submission',assignment_id,score,status FROM coaching.assignment_students WHERE assignment_id='ab202610-2026-4020-8020-000000000001';
SELECT 'temporary_task_remaining',count(*) FROM coaching.study_plan_tasks WHERE "Id"='ab202610-2026-4020-9020-000000000001';
SELECT 'test_assignments',status,count(*) FROM coaching.assignments WHERE id::text LIKE 'ab202610-2026-4020-8020-%' GROUP BY status;
COMMIT;
SQL
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='coaching_tracking_probe_20261020'") == 0 ]]
for service in coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'
 docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" > "final-$service.log" 2>&1
 printf '%s post-release error lines: ' "$service"
 grep -Ec '(\[.* ERR\]|fail:|Unhandled exception)' "final-$service.log" || true
done
while read -r name id; do
 case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
 [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
done < containers.before.txt
date -u +%FT%TZ > live-verified.txt
mkdir -p public-verification
cp prepared.txt built.txt drill-verified.txt deployed.txt live-verified.txt history-verified.log live-counts.txt drill-probe.log portals-build.log public-assets.log previous-*-image.txt built-*-image.txt fixture-seeded.txt fixture-cleaned.txt public-verification/
tar -czf public-verification.tar.gz public-verification
echo 'Live verification complete; every original row, plan, completion and outcome link is preserved.'

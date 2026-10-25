set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-development-20261020
test -s deployed.txt; test -s api-deployed.txt
bash preservation.sh after; bash preservation.sh compare
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-counts.txt
BEGIN READ ONLY;
SELECT 'active_plan',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'assignment',id,title,status FROM coaching.assignments WHERE id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
SELECT 'submission',student_id,score,status FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
COMMIT;
SQL
for service in coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'
 docker logs --since "$(cat api-deployed.txt)" "eduivme-production-$service-1" > "final-$service.log" 2>&1
 printf '%s post-fix error lines: ' "$service"
 grep -Ec '(\[.* ERR\]|fail:|Unhandled exception)' "final-$service.log" || true
done
while read -r name id; do
 [[ "$name" == eduivme-production-coaching-service-1 ]] && continue
 [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
done < api-containers.before.txt
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='coaching_development_probe_20261020'") == 0 ]]
date -u +%FT%TZ > live-verified.txt
mkdir -p public-verification
cp deployed.txt api-deployed.txt api-drill-verified.txt live-verified.txt coaching.before.txt coaching.after.txt identity.before.txt identity.after.txt notification.before.txt notification.after.txt live-counts.txt api-probe-after.log portals-build.log public-verification/
tar -czf public-verification.tar.gz public-verification
echo 'All live test reads preserved every protected row; probe database removed; verification artifacts exclude credentials and backups.'

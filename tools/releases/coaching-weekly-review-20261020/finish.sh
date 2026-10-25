set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-review-20261020
test -s deployed.txt
bash deploy.sh verify
bash history.sh after; bash history.sh compare | tee history-verified.log
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-counts.txt
BEGIN READ ONLY;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM coaching.weekly_reflections WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "Successes" LIKE 'CANLI HAFTALIK TEST 20261020%' AND "Rating"=4) THEN RAISE EXCEPTION 'Student reflection missing'; END IF;
 IF NOT EXISTS (SELECT 1 FROM coaching.weekly_decisions WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "TeacherId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10' AND "Title"='CANLI HAFTALIK TEST 20261020 · Paragraf tekrarı' AND "Status"='Completed' AND "CompletedAt" IS NOT NULL AND jsonb_array_length("HistoryJson")=3) THEN RAISE EXCEPTION 'Decision or its full history missing'; END IF;
END $$;
SELECT 'reflection',"WeekStart","Version","Rating" FROM coaching.weekly_reflections WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "Successes" LIKE 'CANLI HAFTALIK TEST 20261020%';
SELECT 'decision',"ReviewWeekStart","DueOn","Version","Status",jsonb_array_length("HistoryJson") FROM coaching.weekly_decisions WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "Title"='CANLI HAFTALIK TEST 20261020 · Paragraf tekrarı';
SELECT 'active_plan',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'original_submission',score,status FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
COMMIT;
SQL
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='coaching_weekly_probe_20261020'") == 0 ]]
for service in coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'
 docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" > "final-$service.log" 2>&1
 printf '%s post-release error lines: ' "$service"; grep -Ec '(\[.* ERR\]|fail:|Unhandled exception)' "final-$service.log" || true
done
date -u +%FT%TZ > live-verified.txt
mkdir -p public-verification
cp prepared.txt built.txt drill-verified.txt deployed.txt live-verified.txt history-verified.log live-counts.txt drill-probe.log portals-build.log public-assets.log previous-*-image.txt built-*-image.txt public-verification/
tar -czf public-verification.tar.gz public-verification
echo 'Live weekly workflow verified; every original account, plan, study and outcome link preserved.'

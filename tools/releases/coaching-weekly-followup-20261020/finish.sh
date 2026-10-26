set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-followup-20261020
bash deploy.sh verify
bash history.sh after; bash history.sh compare | tee history-verified.log
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-counts.txt
BEGIN READ ONLY;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM coaching.weekly_reflections WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "WeekStart"='2026-10-05' AND "Successes" LIKE 'CANLI BİLDİRİM TESTİ 20261020%' AND "Rating"=4 AND "Version"=0) THEN RAISE EXCEPTION 'Reflection or no-op version mismatch'; END IF;
 IF NOT EXISTS (SELECT 1 FROM coaching.weekly_decisions WHERE "Id"='78c885b5-d9ab-41a0-9aed-8a42952c09da' AND "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "TeacherId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10' AND "ReviewWeekStart"='2026-10-05' AND "DueOn"='2026-10-17' AND "Version"=2 AND "Status"='Completed' AND "CompletedAt" IS NOT NULL AND jsonb_array_length("HistoryJson")=3) THEN RAISE EXCEPTION 'Decision/history mismatch'; END IF;
END $$;
SELECT 'new_reflection',"WeekStart","Version","Rating" FROM coaching.weekly_reflections WHERE "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "WeekStart"='2026-10-05';
SELECT 'new_decision',"ReviewWeekStart","DueOn","Version","Status",jsonb_array_length("HistoryJson") FROM coaching.weekly_decisions WHERE "Id"='78c885b5-d9ab-41a0-9aed-8a42952c09da';
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'active_plan',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'original_submission',score,status FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
COMMIT;
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db <<'SQL' | tee live-notifications.txt
BEGIN READ ONLY;
DO $$ BEGIN
 IF (SELECT count(*) FROM public."Notifications" WHERE "Type"='WeeklyReflectionSaved' AND "UserId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10' AND "RelatedEntityId"='weekly:473bd2d6-e3db-47c4-be56-eeedec0022ed:2026-10-05')<>1 THEN RAISE EXCEPTION 'Reflection notification count mismatch'; END IF;
 IF (SELECT count(*) FROM public."Notifications" WHERE "Type"='WeeklyDecisionCreated' AND "UserId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "RelatedEntityId"='weekly:473bd2d6-e3db-47c4-be56-eeedec0022ed:2026-10-05:78c885b5-d9ab-41a0-9aed-8a42952c09da')<>1 THEN RAISE EXCEPTION 'Created notification count mismatch'; END IF;
 IF (SELECT count(*) FROM public."Notifications" WHERE "Type"='WeeklyDecisionUpdated' AND "UserId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "RelatedEntityId"='weekly:473bd2d6-e3db-47c4-be56-eeedec0022ed:2026-10-05:78c885b5-d9ab-41a0-9aed-8a42952c09da')<>2 THEN RAISE EXCEPTION 'Update notification count mismatch'; END IF;
 IF EXISTS (SELECT 1 FROM public."Notifications" WHERE "Type" LIKE 'Weekly%' AND ("Title" LIKE '%CANLI BİLDİRİM TESTİ%' OR "Message" LIKE '%CANLI BİLDİRİM TESTİ%' OR "Message" LIKE '%Test kaydı:%')) THEN RAISE EXCEPTION 'Private free text copied into notification'; END IF;
END $$;
SELECT "Type",count(*),min("CreatedAt"),max("CreatedAt") FROM public."Notifications" WHERE "Type" LIKE 'Weekly%' GROUP BY "Type" ORDER BY "Type";
COMMIT;
SQL
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='coaching_weekly_followup_probe_20261020'") == 0 ]]
docker exec rabbitmq rabbitmqctl -q list_queues name consumers messages_ready messages_unacknowledged --formatter json | jq '[.[] | select(.name | startswith("coaching-weekly-"))]' > weekly-queues.json
jq -e 'length>=2 and all(.[]; .messages_ready==0 and .messages_unacknowledged==0)' weekly-queues.json >/dev/null
for service in notification-service coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}} restarts={{.RestartCount}}'
 docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" > "final-$service.log" 2>&1
 count=$(grep -Ec '(\[.* ERR\]|fail:|Unhandled exception)' "final-$service.log" || true)
 printf '%s post-release error lines: %s\n' "$service" "$count"; [[ "$count" == 0 ]]
done
date -u +%FT%TZ > live-verified.txt
mkdir -p public-verification
cp prepared.txt built.txt drill-verified.txt deployed.txt live-verified.txt history-verified.log live-counts.txt live-notifications.txt weekly-queues.json drill-probe.log portals-build.log public-assets.log previous-*-image.txt built-*-image.txt public-verification/
tar -czf public-verification.tar.gz public-verification
echo 'Weekly live workflows and notification counts verified; every original record preserved.'

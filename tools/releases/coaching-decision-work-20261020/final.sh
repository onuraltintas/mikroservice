set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
bash history.sh after; bash history.sh compare
for s in coaching-service admin-panel staff-portal; do
 expected=$(<built-coaching-service-fixed.txt); [[ "$s" == coaching-service ]] || expected=$(<"focus-built-$s.txt")
 [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$expected" ]]
 docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'
done
while read -r name id; do
 case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
 [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
done < containers.before.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-results.txt
BEGIN READ ONLY;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM coaching.weekly_decisions WHERE "Title"='CANLI KARAR BAĞLANTI TESTİ · 21.10.2026' AND "StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "TeacherId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10' AND "StudyTaskId"='6f8117eb-59fd-4268-b69e-53a9d804025d' AND "AssignmentId"='24602014-478c-4ace-b2e5-9d682877cffa' AND "Status"='Completed' AND "CompletedAt" IS NOT NULL) THEN RAISE EXCEPTION 'Final decision or links mismatch'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='6f8117eb-59fd-4268-b69e-53a9d804025d' AND "RevisionId"='a4cea155-5485-4db6-9f52-1ec4df8080c3' AND "PlannedDate"='2026-10-23' AND "PlannedStartMinute"=660 AND "PlannedMinutes"=30 AND "IsCompleted" AND "ActualMinutes"=25) THEN RAISE EXCEPTION 'New test task state mismatch'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='294578cc-e8e0-431d-a25b-a0a0d1674ca5' AND "IsCompleted" AND "ActualMinutes"=55 AND "PlannedDate"='2026-10-19') THEN RAISE EXCEPTION 'Original completed study changed'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND score=95) THEN RAISE EXCEPTION 'Original graded assignment changed'; END IF;
 IF (SELECT count(*) FROM coaching.study_plan_revisions)<>44 OR (SELECT count(*) FROM coaching.study_plan_tasks)<>62017 OR (SELECT count(*) FROM coaching.study_plan_task_outcomes)<>61979 THEN RAISE EXCEPTION 'Unexpected plan/task/outcome totals'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3' AND "Version"=10 AND "IsActive") THEN RAISE EXCEPTION 'Expected only create/move/complete plan version changes'; END IF;
END $$;
SELECT 'test decision',"Id","Version","Status",jsonb_array_length("HistoryJson") FROM coaching.weekly_decisions WHERE "Title"='CANLI KARAR BAĞLANTI TESTİ · 21.10.2026';
SELECT 'test assignment',assignment_id,status,score FROM coaching.assignment_students WHERE assignment_id='24602014-478c-4ace-b2e5-9d682877cffa';
SELECT 'original assignment',assignment_id,status,score FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'outcome links',count(*) FROM coaching.study_plan_task_outcomes;
COMMIT;
SQL
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c "SELECT count(*) FROM coaching.assignment_students WHERE assignment_id='24602014-478c-4ace-b2e5-9d682877cffa' AND status::text='Submitted'") == 1 ]]
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='coaching_decision_work_probe_20261020'") == 0 ]]
docker logs --since "$(<validation-fixed.txt)" eduivme-production-coaching-service-1 > after-fix-api.log 2>&1
if grep -E '\bERR\]|\bFTL\]|Unhandled exception' after-fix-api.log > after-fix-errors.txt; then echo 'Unexpected API errors after fix'; exit 1; fi
bash public-check.sh
if test ! -e initial-production.compose.json; then cp production.compose.json initial-production.compose.json; fi
cp final.compose.json production.compose.json
python3 - <<'PY'
from pathlib import Path
from datetime import datetime,timezone
started=datetime.fromisoformat(Path('validation-fixed.txt').read_text().strip().replace('Z','+00:00'))
print('API observation since validation fix: '+str(round((datetime.now(timezone.utc)-started).total_seconds()/60,1))+' minutes')
PY
date -u +%FT%TZ > final-verified.txt
echo 'Live links, approval, movement, completion/submission and preservation checks passed; no API errors since the validation fix.'

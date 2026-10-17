set -euo pipefail
release=/var/lib/eduivme/releases/coaching-restore-profile-20261017
cd "$release"
{
for service in coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}'
 count=$(docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" 2>&1 | grep -Eic 'Unhandled exception|fail:|\[ERR\]|FATAL' || true)
 printf '%s application_error_markers=%s\n' "$service" "$count"
 [[ "$count" == 0 ]]
done
curl -fsSL https://onuraltintas.net/staff/ > /tmp/coaching-restore-staff-public.html
python3 - <<'PY'
import re,urllib.request
s=open('/tmp/coaching-restore-staff-public.html').read()
assert '<base href="/staff/">' in s
for p in re.findall(r'(?:src|href)="((?:main-|chunk-|styles-)[^" ]+\.(?:js|css))"',s):
 r=urllib.request.urlopen('https://onuraltintas.net/staff/'+p,timeout=10)
 assert r.status==200
 print('staff_asset',p,r.status,r.headers.get_content_type())
PY
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('annualRevision','2f7e2d07-e044-471d-aedc-dfe004df7e2e','tasks',count(*),'plannedMinutes',sum("PlannedMinutes"),'completed',count(*) FILTER(WHERE "IsCompleted"),'actualMinutes',sum("ActualMinutes")) FROM coaching.study_plan_tasks WHERE "RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SELECT json_build_object('annualUniqueOutcomes',count(DISTINCT o."OutcomeId")) FROM coaching.study_plan_task_outcomes o JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e';
SELECT json_build_object('orphanedOutcomes',count(*)) FROM coaching.study_plan_task_outcomes o LEFT JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."Id" IS NULL;
SQL
} > diagnostics.txt
date -u +%FT%TZ > diagnostics-verified.txt
cat diagnostics.txt
if test -f monitor-verified.txt; then cat monitor-verified.txt; else printf 'health_samples='; grep -Ec '^20[0-9]{2}-' monitor.log || true; tail -7 monitor.log; fi

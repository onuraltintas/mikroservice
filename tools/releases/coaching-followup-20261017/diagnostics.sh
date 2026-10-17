set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-followup-20261017
{
for service in notification-service coaching-service staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}'
 count=$(docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" 2>&1 | grep -Eic 'Unhandled exception|fail:|\[ERR\]|FATAL' || true)
 printf '%s application_error_markers=%s\n' "$service" "$count"; [[ "$count" == 0 ]]
done
docker exec rabbitmq rabbitmqctl -q list_queues name consumers messages --formatter json | jq '[.[]|select(.name=="coaching-study-plan-updated" or .name=="coaching-study-plan-updated_error")]'
curl -fsSL https://onuraltintas.net/staff/ > /tmp/coaching-followup-staff-public.html
python3 - <<'PY'
import re,urllib.request
s=open('/tmp/coaching-followup-staff-public.html').read()
assert '<base href="/staff/">' in s
for p in re.findall(r'(?:src|href)="((?:main-|chunk-|styles-)[^" ]+\.(?:js|css))"',s):
 r=urllib.request.urlopen('https://onuraltintas.net/staff/'+p,timeout=10)
 assert r.status==200
 print('staff_asset',p,r.status,r.headers.get_content_type())
PY
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('pending',count(*),'overdue',count(*) FILTER(WHERE a.due_date<now())) FROM coaching.assignment_students s JOIN coaching.assignments a ON a.id=s.assignment_id WHERE s.student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND a.teacher_id IN (SELECT "TeacherUserId" FROM coaching.teacher_student_assignments WHERE "StudentUserId"=s.student_id AND "IsActive") AND a.status='Active' AND s.status IN ('Assigned','InProgress') AND s.submitted_at IS NULL;
SELECT json_build_object('orphanedOutcomeLinks',count(*)) FROM coaching.study_plan_task_outcomes o LEFT JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."Id" IS NULL;
SQL
} > diagnostics.txt
date -u +%FT%TZ > diagnostics-verified.txt
cat diagnostics.txt

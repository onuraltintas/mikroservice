set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-submission-20261017
db_user=$(docker exec postgres printenv POSTGRES_USER)
started=$(cat /var/lib/eduivme/releases/coaching-followup-20261017/submission-verification/started.txt)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db -v "started=$started" <<'SQL' > notification-result.json
SELECT json_build_object('submissionNotifications',count(*),'correctTeacher',bool_and("UserId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10'),'correctAssignment',bool_and("RelatedEntityId"='c260e5c4-facd-42cd-af3a-69aa0b9cc67f')) FROM public."Notifications" WHERE "Type"='AssignmentSubmitted' AND "CreatedAt">=:'started'::timestamptz;
SQL
jq -e '.submissionNotifications==1 and .correctTeacher and .correctAssignment' notification-result.json >/dev/null
{
cat notification-result.json
for service in coaching-service staff-portal notification-service;do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}}'
 count=$(docker logs --since "$(cat deployed.txt)" "eduivme-production-$service-1" 2>&1|grep -Eic 'Unhandled exception|fail:|\[ERR\]|FATAL'||true)
 printf '%s errors=%s\n' "$service" "$count";[[ "$count" == 0 ]]
done
curl -fsSL https://onuraltintas.net/staff/ > staff-public.html
python3 - <<'PY'
import re,urllib.request
s=open('staff-public.html').read();assert '<base href="/staff/">' in s
for p in re.findall(r'(?:src|href)="((?:main-|chunk-|styles-)[^" ]+\.(?:js|css))"',s):
 r=urllib.request.urlopen('https://onuraltintas.net/staff/'+p,timeout=10);assert r.status==200
 print('asset',p,r.status,r.headers.get_content_type())
PY
} > diagnostics.txt
date -u +%FT%TZ > verified.txt
cat diagnostics.txt

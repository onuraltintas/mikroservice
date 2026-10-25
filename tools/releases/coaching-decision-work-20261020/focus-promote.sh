set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
test -s focus-built.txt; test ! -e focus-deployed.txt
[[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$(<built-coaching-service-fixed.txt)" ]]
cmp candidate-student-study-plans.ts source/admin-panel/src/app/features/coaching-portal/pages/student-study-plans.component.ts
python3 - <<'PY'
import json,subprocess
from pathlib import Path
for s in ['admin-panel','staff-portal']:
 old,new=json.loads(subprocess.check_output(['docker','image','inspect',Path('built-'+s+'.txt').read_text().strip(),Path('focus-built-'+s+'.txt').read_text().strip()]))
 for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal','Healthcheck']:
  assert old['Config'].get(key)==new['Config'].get(key),(s,key)
print('Focus fix runtime configurations preserved.')
PY
jq '.services["admin-panel"].image="eduivme/admin-panel:coaching-decision-work-focus-20261021"|.services["staff-portal"].image="eduivme/staff-portal:coaching-decision-work-focus-20261021"' fixed.compose.json > final.compose.json
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
recover(){ local code=$?; trap - EXIT; if ((code!=0)); then "${compose[@]}" -f fixed.compose.json up -d --no-deps --pull never admin-panel staff-portal > focus-rollback.log 2>&1 || true; fi; exit "$code"; }; trap recover EXIT
for s in admin-panel staff-portal; do
 [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$(<"built-$s.txt")" ]]
 "${compose[@]}" -f final.compose.json up -d --no-deps --pull never "$s" > "focus-$s-promote.log" 2>&1
 port=4000; path=/; [[ "$s" != staff-portal ]] || { port=80; path=/staff/; }
 for attempt in $(seq 1 30); do
  ip=$(docker inspect "eduivme-production-$s-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then break; fi; sleep 2
 done
 curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null
done
bash history.sh after; bash history.sh compare
bash public-check.sh
date -u +%FT%TZ > focus-deployed.txt; trap - EXIT
echo 'Linked-task focus deployed to both portals; API and existing records preserved.'

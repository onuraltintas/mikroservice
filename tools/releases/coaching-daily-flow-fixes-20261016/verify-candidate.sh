#!/usr/bin/env bash
set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-daily-flow-fixes-20261016-r2
candidate=coaching-daily-flow-preview-20261016-r2
expected=$(docker image inspect eduivme/admin-panel:coaching-daily-flow-fixes-20261016-r2 --format '{{.Id}}')
[[ $(docker inspect "$candidate" --format '{{.Image}}') == "$expected" ]]
[[ $(docker inspect "$candidate" --format '{{.State.Running}}') == true ]]
[[ $(docker inspect "$candidate" --format '{{.RestartCount}}') == 0 ]]
[[ $(docker inspect "$candidate" --format '{{len .HostConfig.PortBindings}}') == 0 ]]
ip=$(docker inspect "$candidate" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
[[ "$ip" =~ ^[0-9.]+$ ]]
for route in auth/login coaching-portal; do
  file="candidate-${route//\//-}.html"
  code=$(curl -sS --max-time 15 -H 'Host: eduivme.com' -o "$file" -w '%{http_code}' "http://$ip:4000/$route")
  echo "Candidate /$route HTTP $code"
  [[ "$code" == 200 ]]
done
main=$(grep -oE 'src="main-[^"]+\.js"' candidate-auth-login.html | head -1 | cut -d'"' -f2)
[[ "$main" =~ ^main-[A-Za-z0-9_-]+\.js$ ]]
code=$(curl -sS --max-time 15 -H 'Host: eduivme.com' -o /dev/null -w '%{http_code}' "http://$ip:4000/$main")
echo "Candidate /$main HTTP $code"
[[ "$code" == 200 ]]
docker logs "$candidate" > candidate-runtime.log 2>&1
if grep -Ei 'ERROR|Exception|Unhandled' candidate-runtime.log; then exit 1; fi
docker inspect "$candidate" --format 'Candidate status={{.State.Status}} restarts={{.RestartCount}} image={{.Image}}'
date -u +%FT%TZ > candidate-explicit-health.txt
cat candidate-explicit-health.txt

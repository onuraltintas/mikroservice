#!/usr/bin/env bash
set -euo pipefail
cd /var/lib/eduivme/releases/coaching-learning-20261016
test -s deployed.txt
: > monitor.txt
for sample in $(seq 0 30); do
 stamp=$(date -u +%FT%TZ)
 for service in identity-service coaching-service admin-panel staff-portal; do
  name="eduivme-production-$service-1"
  docker inspect "$name" --format '{{.State.Running}} {{.RestartCount}}' | awk -v stamp="$stamp" -v service="$service" '{print stamp,service,"running=" $1,"restarts=" $2; if($1!="true" || $2!=0)exit 1}' >> monitor.txt
 done
 for path in /auth/login /coaching-portal/setup /staff/; do
  result=$(curl -sSL --max-time 15 -o /dev/null -w '%{http_code} %{time_total}' "https://eduivme.com$path")
  [[ "$result" == 200\ * ]]
  printf '%s path=%s http=%s\n' "$stamp" "$path" "$result" >> monitor.txt
 done
 (( sample == 30 )) || sleep 30
done
printf 'MONITOR_PASSED samples=31 durationSeconds=900\n'

#!/usr/bin/env bash
set -euo pipefail
umask 077
[[ "$PWD" == /var/lib/eduivme/releases/subscription-requirement-20261014 ]]
test -s deployed.txt
started=$(date +%s)
while (( $(date +%s) - started < 900 )); do
  bash deploy.sh validate >> monitor-health.log 2>&1
  for container in eduivme-production-speed-reading-service-1 eduivme-production-admin-panel-1; do
    [[ $(docker inspect "$container" --format '{{.RestartCount}}') == 0 ]]
  done
  for url in https://eduivme.com/auth/login https://masterhizliokuma.com/auth/login https://onuraltintas.net/staff/; do
    curl -fsS --max-time 15 -o /dev/null -w '%{http_code} %{time_total}\n' "$url" >> monitor-public-http.log
  done
  date -u +%FT%TZ >> monitor-health.log
  sleep 30
done
date -u +%FT%TZ > monitor-complete.txt
cat monitor-complete.txt

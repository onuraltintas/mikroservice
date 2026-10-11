#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/comprehension-20261011-v1
backend=eduivme-production-speed-reading-service-1
since=$(cat "$release/deployed-at.txt")
restart_before=$(docker inspect -f '{{.RestartCount}}' "$backend")
for sample in $(seq 1 30); do
  status=$(curl -fsS --connect-timeout 5 --max-time 10 http://172.31.0.7:8080/health/ready)
  [[ "$status" == Healthy ]]
  [[ $(docker inspect -f '{{.State.Running}}' "$backend") == true ]]
  [[ $(docker inspect -f '{{.RestartCount}}' "$backend") == "$restart_before" ]]
  printf '%s|%s|%s|restart=%s\n' "$(date -Is)" "$sample" "$status" "$restart_before"
  sleep 30
done
markers=$(docker logs --since "$since" "$backend" 2>&1 | grep -Eic 'Unhandled exception|\[FTL\]|\[ERR\]|fail:' || true)
[[ "$markers" == 0 ]]
printf 'MONITOR_OK: 15 minutes / 30 healthy readiness samples / unchanged restart count / no severe log marker\n'

#!/usr/bin/env bash
set -euo pipefail
api=eduivme-production-speed-reading-service-1
monitor_start="$(date --iso-8601=seconds)"
initial_restarts="$(docker inspect -f '{{.RestartCount}}' "$api")"
for sample in $(seq 1 30); do
  state="$(curl --fail --silent --show-error --max-time 10 http://172.31.0.7:8080/health/ready)"
  test "$state" = Healthy
  test "$(docker inspect -f '{{.RestartCount}}' "$api")" = "$initial_restarts"
  printf '%s|%s|Healthy|restart=%s\n' "$(date --iso-8601=seconds)" "$sample" "$initial_restarts"
  sleep 30
done
monitor_logs="$(docker logs --since "$monitor_start" "$api" 2>&1)"
if printf '%s\n' "$monitor_logs" | grep -Eqi '(^|[[:space:]])(fail|crit|fatal):|Unhandled exception'; then
  echo 'Severe log marker detected; inspect securely before closing release' >&2
  exit 1
fi
echo 'MONITOR_OK: 30 healthy samples / 15 minutes / no restart or severe log marker'

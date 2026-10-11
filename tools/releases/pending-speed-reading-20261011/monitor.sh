#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/pending-speed-reading-20261011
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
since=$(<"$release/deployed-at.txt")
restart_before=$(docker inspect -f '{{.RestartCount}}' "$backend")
ip=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "$backend" | awk '{print $1}')
[[ "$ip" =~ ^[0-9.]+$ ]]
for sample in $(seq 1 30); do
  status=$(curl -fsS --connect-timeout 5 --max-time 10 "http://$ip:8080/health/ready")
  [[ "$status" == Healthy ]]
  [[ $(docker inspect -f '{{.State.Running}}' "$backend") == true ]]
  [[ $(docker inspect -f '{{.RestartCount}}' "$backend") == "$restart_before" ]]
  [[ $(docker inspect -f '{{.State.Health.Status}}' "$frontend") == healthy ]]
  printf '%s|%s|%s|restart=%s\n' "$(date -Is)" "$sample" "$status" "$restart_before"
  sleep 30
done
logs=$(docker logs --since "$since" "$backend" 2>&1)
markers=$(printf '%s\n' "$logs" | grep -Eic 'Unhandled exception|\[FTL\]|\[ERR\]|fail:' || true)
[[ "$markers" == 0 ]]
printf 'MONITOR_OK: 15 minutes / 30 healthy samples / stable restart count / no severe error\n'

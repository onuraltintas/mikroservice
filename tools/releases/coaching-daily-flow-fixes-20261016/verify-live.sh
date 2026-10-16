#!/usr/bin/env bash
set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-daily-flow-fixes-20261016-r2
bash deploy.sh validate
container=eduivme-production-admin-panel-1
for route in auth/login coaching-portal main-2BSP46S5.js; do
  code=$(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' "https://eduivme.com/$route")
  echo "Live /$route HTTP $code"
  [[ "$code" == 200 ]]
done
docker logs --since "$(<deployed.txt)" "$container" > live-runtime.log 2>&1
if grep -Ei 'ERROR|Exception|Unhandled' live-runtime.log; then exit 1; fi
echo 'Frontend error matches: 0'
docker inspect "$container" --format 'Frontend status={{.State.Status}} restarts={{.RestartCount}} user={{.Config.User}} image={{.Image}}'
for service in identity coaching notification; do
  name="eduivme-production-$service-service-1"
  ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  [[ "$ip" =~ ^[0-9.]+$ ]]
  response=$(curl -fsS --max-time 10 "http://$ip:8080/health/ready")
  [[ "$response" == *Healthy* ]]
  echo "$service readiness: Healthy"
done
date -u +%FT%TZ

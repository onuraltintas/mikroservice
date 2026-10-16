set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-outcomes-20261016
test -s deployed.txt
for sample in $(seq 1 11); do
  date -u +%FT%TZ
  for service in coaching-service admin-panel staff-portal; do
    docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}}'
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.State.Running}} {{.RestartCount}}') == 'true 0' ]]
  done
  for url in https://eduivme.com/auth/login https://eduivme.com/coaching-portal/setup https://eduivme.com/staff/; do
    code=$(curl -sS -L --max-time 10 -o /dev/null -w '%{http_code}' "$url")
    printf '%s %s\n' "$code" "$url"; [[ "$code" == 200 ]]
  done
  [[ "$sample" == 11 ]] || sleep 30
done > monitor.log
date -u +%FT%TZ > monitor-verified.txt
echo 'Eleven health samples passed over at least five minutes; no restarts and all public routes HTTP 200.'

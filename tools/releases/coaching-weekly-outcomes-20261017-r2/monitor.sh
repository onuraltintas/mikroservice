set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2
test -s deployed.txt
for sample in $(seq 1 31); do
  date -u +%FT%TZ
  for service in coaching-service admin-panel; do
    docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}}'
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.State.Running}} {{.RestartCount}}') == 'true 0' ]]
  done
  for url in https://eduivme.com/auth/login https://eduivme.com/coaching-portal/weekly-plan https://eduivme.com/coaching-portal/weekly-plan-preview https://eduivme.com/coaching-portal/study-plans; do
    code=$(curl -sS -L --max-time 10 -o /dev/null -w '%{http_code}' "$url")
    printf '%s %s\n' "$code" "$url"; [[ "$code" == 200 ]]
  done
  [[ "$sample" == 31 ]] || sleep 30
done > monitor.log
date -u +%FT%TZ > monitor-verified.txt
echo 'Thirty-one health samples passed over at least fifteen minutes; no restarts and all public routes HTTP 200.'

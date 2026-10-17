set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2
start=$(<deployed.txt)
for service in coaching-service admin-panel; do
  count=$(docker logs --since "$start" "eduivme-production-$service-1" 2>&1 | awk '/\[ERR\]|\[FTL\]|Unhandled exception|"Level":"Error"|"Level":"Fatal"/{n++} END{print n+0}')
  printf '%s error-log-lines=%s\n' "$service" "$count"
  [[ "$count" == 0 ]]
done
for path in /api/coaching/study-planning/weekly-draft /api/coaching/catalog/exams /api/coaching/study-planning/weekly-outcome-plans/options; do
  status=$(curl -sS --max-time 10 -o /dev/null -w '%{http_code}' "https://eduivme.com$path")
  printf 'anonymous %s %s\n' "$status" "$path"; [[ "$status" == 401 ]]
done

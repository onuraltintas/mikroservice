set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2
start=$(<deployed.txt)
for service in coaching-service admin-panel staff-portal; do
 count=$(docker logs --since "$start" "eduivme-production-$service-1" 2>&1 | awk '/\[ERR\]|\[FTL\]|Unhandled exception|"Level":"Error"|"Level":"Fatal"/{n++} END{print n+0}')
 printf '%s application-error-lines=%s\n' "$service" "$count"; [[ "$count" == 0 ]]
done > diagnostics.txt
cat diagnostics.txt
test "$(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}')" = "$(awk '$1=="eduivme-production-coaching-service-1"{print $2}' baseline/containers.txt)"
date -u +%FT%TZ > diagnostics-verified.txt

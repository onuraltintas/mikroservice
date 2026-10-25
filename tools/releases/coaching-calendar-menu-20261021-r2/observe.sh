set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
started=$(<promotion-start.txt)
docker logs --since "$started" eduivme-production-coaching-service-1 > api-observation.log 2>&1
if grep -E '\bERR\]|\bFTL\]|Unhandled exception' api-observation.log > api-errors.txt; then cat api-errors.txt; exit 1; fi
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'; done
printf 'API error count: '; wc -l < api-errors.txt
printf 'Migration rehearsal: '; cat drill-verified.txt
printf 'Promotion: '; cat deployed.txt

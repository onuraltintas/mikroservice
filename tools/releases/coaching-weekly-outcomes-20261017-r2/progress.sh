set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2
for file in prepared.txt built.txt drill-verified.txt writer-stop.log live-backups.sha256 live-migration.log deployed.txt; do
  if test -e "$file"; then printf '%s bytes=' "$file"; stat -c %s "$file"; fi
done
for service in coaching-service admin-panel; do docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} status={{.State.Status}} restarts={{.RestartCount}}'; done
SQL_STATUS=$(docker ps --filter label=com.docker.compose.oneoff=True --format '{{.Names}} {{.Status}}')
printf '%s\n' "$SQL_STATUS"

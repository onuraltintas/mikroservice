set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-followup-20261020
for service in notification-service coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} {{.State.Status}} {{.Config.Image}} restarts={{.RestartCount}}'
 if test -f "$service-promote.log"; then tail -n 3 "$service-promote.log"; fi
 docker logs --tail 50 "eduivme-production-$service-1" 2>&1 | grep -E '(ERR|fail:|Exception|Now listening|Application started)' | tail -n 8 || true
done
test ! -f rollback.log || tail -n 8 rollback.log

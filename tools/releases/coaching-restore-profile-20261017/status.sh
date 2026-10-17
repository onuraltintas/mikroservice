set -euo pipefail
cd /var/lib/eduivme/releases/coaching-restore-profile-20261017
for f in writer-stop.log live-migration.log coaching-service-promotion.log admin-panel-promotion.log staff-portal-promotion.log deployed.txt; do if test -e "$f"; then echo "$f"; tail -n 5 "$f"; fi; done
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} {{.RestartCount}}'; done

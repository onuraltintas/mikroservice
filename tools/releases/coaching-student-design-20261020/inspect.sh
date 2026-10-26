set -euo pipefail
for service in admin-panel staff-portal coaching-service; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} {{.State.Status}} {{.Config.Image}} restarts={{.RestartCount}}'
done
base=/var/lib/eduivme/releases/coaching-weekly-followup-20261020
test -f "$base/deployed.txt"
test -d "$base/source/admin-panel"
for file in src/app/features/coaching-portal/layout/coaching-portal-layout.component.scss src/app/features/coaching-portal/pages/weekly-review.component.ts src/app/core/services/toaster.service.ts; do
 sha256sum "$base/source/admin-panel/$file"
done
df -h /var/lib/eduivme | tail -1

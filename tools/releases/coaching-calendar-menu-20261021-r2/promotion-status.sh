set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.Config.Image}} {{.State.Status}} restarts={{.RestartCount}}'; done
for log in coaching-service-promote.log admin-panel-promote.log staff-portal-promote.log; do test ! -f "$log" || tail -n 2 "$log"; done

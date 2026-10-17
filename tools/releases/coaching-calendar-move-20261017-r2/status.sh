set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2
for file in deployed.txt live-result.json monitor-verified.txt; do if test -f "$file"; then cat "$file"; fi; done
for service in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}'; done
if test -f monitor.log; then tail -8 monitor.log; fi

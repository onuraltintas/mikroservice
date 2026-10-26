set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
for stage in prepared built drill-verified deployed; do test ! -s "$stage.txt" || printf '%s: %s\n' "$stage" "$(<"$stage.txt")"; done
for log in portals-build.log coaching-service-build.log coaching_practice_drill_20261020-migrate.log; do test ! -f "$log" || { echo "$log"; tail -n 3 "$log"; }; done
docker ps --format '{{.Names}}|{{.Status}}' | head -4

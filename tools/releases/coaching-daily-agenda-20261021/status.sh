set -euo pipefail
cd /var/lib/eduivme/releases/coaching-daily-agenda-20261021
for file in prepared.txt built.txt drill-verified.txt deployed.txt; do if test -s "$file"; then printf '%s ' "$file"; cat "$file"; fi; done
tail -n 8 build-driver.log
tail -n 5 portals-build.log

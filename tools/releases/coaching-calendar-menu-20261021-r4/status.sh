set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4
for file in prepared.txt built.txt deployed.txt; do if test -s "$file"; then printf '%s ' "$file"; cat "$file"; fi; done
tail -n 8 build-driver.log
tail -n 5 portals-build.log

set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-review-20261020
for phase in prepared built drill-verified deployed live-verified; do if test -s "$phase.txt"; then printf '%s: ' "$phase"; cat "$phase.txt"; fi; done
if test -f portals-build.log; then tail -n 10 portals-build.log; fi
if test -f build-driver.log; then tail -n 6 build-driver.log; fi

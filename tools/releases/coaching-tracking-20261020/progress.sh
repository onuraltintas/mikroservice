set -euo pipefail
cd /var/lib/eduivme/releases/coaching-tracking-20261020
for name in prepared built drill-verified deployed; do if test -s "$name.txt"; then printf '%s ' "$name"; cat "$name.txt"; fi; done
tail -6 build-driver.log
if test -f portals-build.log; then tail -8 portals-build.log; fi
if test -f source-overlay.diff; then grep '^[-+][^-+]' source-overlay.diff | head -45 || true; fi

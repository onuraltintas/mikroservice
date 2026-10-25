set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
for marker in prepared built drill-verified deployed; do test ! -s "$marker.txt" || printf '%s: %s\n' "$marker" "$(<"$marker.txt")"; done
tail -n 5 build-driver.log 2>/dev/null || true
tail -n 6 portals-build.log 2>/dev/null || true

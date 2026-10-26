set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
if test -s built.txt; then echo 'Images ready'; else tail -n 8 portals-build.log; fi
if test -s built.txt; then for s in coaching-service admin-panel staff-portal; do printf '%s ' "$s"; cat "built-$s.txt"; done; fi

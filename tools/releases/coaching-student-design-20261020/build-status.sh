set -euo pipefail
cd /var/lib/eduivme/releases/coaching-student-design-20261020-r3
tail -n 8 build-driver.log
if test -f admin-panel-build.log; then tail -n 10 admin-panel-build.log; fi
docker ps --filter name=coaching-student-design-builder --format '{{.Names}} {{.Status}}'
if test -s built.txt; then cat built.txt; fi

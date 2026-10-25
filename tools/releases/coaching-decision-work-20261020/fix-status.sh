set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
tail -n 25 http-validation-fix/build.log
if test -s fixed-promote.log; then tail -n 10 fixed-promote.log; fi
docker inspect eduivme-production-coaching-service-1 --format '{{.Image}} {{.State.Status}}'

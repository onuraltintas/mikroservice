set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-yks-regression-20261020
test ! -f api-fix-build.log || tail -n 12 api-fix-build.log
test ! -f api-fix-promote.log || tail -n 4 api-fix-promote.log
docker inspect eduivme-production-coaching-service-1 --format '{{.Config.Image}}'

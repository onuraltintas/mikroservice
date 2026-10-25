set -euo pipefail
cd /var/lib/eduivme/releases/unified-target-catalog-20261020
awk '$2 != "deploy.sh"' payload.sha256 > next-payload.sha256
sha256sum deploy.sh >> next-payload.sha256
mv next-payload.sha256 payload.sha256
bash -n deploy.sh
bash deploy.sh build

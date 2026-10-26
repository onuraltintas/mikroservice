set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
bash -n deploy.sh
sha256sum -c payload.sha256 > final-source-check.log
bash deploy.sh promote
bash deploy.sh verify

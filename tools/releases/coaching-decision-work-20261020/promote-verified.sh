set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
test -s drill-verified.txt
bash -n deploy-v2.sh
bash deploy-v2.sh promote
bash deploy-v2.sh verify

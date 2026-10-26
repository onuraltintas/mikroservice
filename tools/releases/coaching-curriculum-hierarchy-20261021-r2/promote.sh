set -euo pipefail
cd /var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021-r2
bash deploy.sh promote
bash deploy.sh verify

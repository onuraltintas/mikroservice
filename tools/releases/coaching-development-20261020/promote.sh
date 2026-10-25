set -euo pipefail
cd /var/lib/eduivme/releases/coaching-development-20261020
bash deploy.sh promote
bash deploy.sh verify

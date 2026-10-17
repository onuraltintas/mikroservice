set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2
bash deploy.sh promote
bash deploy.sh validate

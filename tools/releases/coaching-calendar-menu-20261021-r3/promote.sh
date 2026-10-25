set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r3
bash deploy.sh promote
bash deploy.sh verify

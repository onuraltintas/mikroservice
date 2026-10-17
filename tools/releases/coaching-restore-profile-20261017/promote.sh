set -euo pipefail
cd /var/lib/eduivme/releases/coaching-restore-profile-20261017
bash deploy.sh promote
bash deploy.sh validate

set -euo pipefail
cd /var/lib/eduivme/releases/coaching-followup-20261017
test -s notification-drill-verified.txt
bash deploy.sh promote
bash deploy.sh validate

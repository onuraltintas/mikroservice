set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-followup-20261020
bash deploy.sh drill
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
echo 'Restored-backup drill, promotion, public JS/CSS and readiness passed.'

set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-review-20261020
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
echo 'Public HTML, JavaScript and stylesheet status/MIME checks passed.'

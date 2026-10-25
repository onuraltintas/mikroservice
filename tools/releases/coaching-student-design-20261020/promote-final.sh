set -euo pipefail
cd /var/lib/eduivme/releases/coaching-student-design-20261020-r3
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
cat deployed.txt
cat preservation.txt
tail -n 6 public-assets.log


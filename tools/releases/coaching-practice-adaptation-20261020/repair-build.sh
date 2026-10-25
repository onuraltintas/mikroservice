set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
sha256sum -c source-repair.sha256
test -s prepared.txt; test ! -e built.txt; test ! -e deployed.txt
tar -xzf source-repair.tar.gz
bash -n deploy.sh
sha256sum -c payload.sha256 > repaired-payload-check.log
bash deploy.sh build
bash deploy.sh drill

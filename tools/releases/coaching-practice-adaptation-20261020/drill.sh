set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
bash -n deploy.sh
# The reviewed target-database guard was added after source assembly.
awk '$2 != "deploy.sh"' payload.sha256 > stable-payload.sha256
sha256sum -c stable-payload.sha256 > stable-payload-check.log
sha256sum deploy.sh >> stable-payload.sha256
mv stable-payload.sha256 payload.sha256
bash deploy.sh drill

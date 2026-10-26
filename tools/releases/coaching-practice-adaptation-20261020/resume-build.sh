set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
# Normalize only the Windows-generated checksum file's line terminators.
sed -i 's/\r$//' payload.sha256
sha256sum -c payload.sha256 > payload-verified.log
test ! -e prepared.txt
bash deploy.sh prepare
cmp source/admin-panel/package-lock.json /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/package-lock.json
bash deploy.sh build
bash deploy.sh drill

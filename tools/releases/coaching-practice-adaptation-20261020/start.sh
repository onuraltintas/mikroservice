set -euo pipefail
umask 077
archive=/var/lib/eduivme/releases/coaching-practice-adaptation-20261020.tar.gz
release=/var/lib/eduivme/releases/coaching-practice-adaptation-20261020
echo 'f274aac1a9c6954f25471a1a07b2fc768c6a97d3273c335ce6fb7fe5599dcfc4  /var/lib/eduivme/releases/coaching-practice-adaptation-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$archive" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
cmp source/admin-panel/package-lock.json /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/package-lock.json
bash deploy.sh build
bash deploy.sh drill

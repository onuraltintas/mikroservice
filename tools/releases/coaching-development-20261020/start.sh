set -euo pipefail
umask 077
archive=/var/lib/eduivme/releases/coaching-development-20261020.tar.gz
release=/var/lib/eduivme/releases/coaching-development-20261020
echo '07223cccf69767277c87c7571c74ee79e1354f377dafc584c8800e7757858cd2  /var/lib/eduivme/releases/coaching-development-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"
df -h /var/lib/eduivme
mkdir -m 700 "$release"
tar -xzf "$archive" -C "$release"
cd "$release"
bash -n deploy.sh
bash -n preservation.sh
bash deploy.sh prepare
bash deploy.sh build

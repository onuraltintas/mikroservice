set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-screen-audit-20261020
echo '82758027b7244b5b3e817f104923395abfaabfe54499b607f722602225833d03  /var/lib/eduivme/releases/coaching-screen-audit-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated screen consistency build started.'

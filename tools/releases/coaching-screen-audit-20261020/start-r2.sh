set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-screen-audit-20261020-r2
echo 'f230df1c4c3389fb3d8d3cc74c03b04c8fc17535168f4404db1dfc45658ad4f6  /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated screen consistency build started.'

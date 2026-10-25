set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-workspace-depth-20261020-r2
echo 'fc092cd0aa42deaca4db2031849fafbcee39a47a96a20e25d3d166ec5bbbc846  /var/lib/eduivme/releases/coaching-workspace-depth-20261020-r2.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated frontend style build started.'

set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-workspace-depth-20261020
echo 'f60c0926b09000bca1b56f8e325f01e44dd7779f46c5aaaf6a1f385aa206c6a6  /var/lib/eduivme/releases/coaching-workspace-depth-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated frontend style build started.'

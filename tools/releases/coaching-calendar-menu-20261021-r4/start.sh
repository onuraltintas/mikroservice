set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4
echo '78bf90dfa5d30d383757fe57dbf04936864bc573f81e0c70597aaf66dc115ad2  /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Frontend hotfix build started.'

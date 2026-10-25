set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r3
echo 'e39fc768f98cca0afc5ee1d9c37eb1e72c68d313afaad5a1284d02baaf349387  /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r3.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Frontend hotfix build started.'

set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
echo '67a46d0f464e094661cd9d62ceb4ab861f96751b0dbbf087e10783a422c4af31  /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated calendar-menu build started.'

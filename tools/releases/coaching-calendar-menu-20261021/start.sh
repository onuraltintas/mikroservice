set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-menu-20261021
echo 'a05d49856f07a99aaab82638a5c8ce4f27301222dfae703245c5b767278fe035  /var/lib/eduivme/releases/coaching-calendar-menu-20261021.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated calendar-menu build started.'

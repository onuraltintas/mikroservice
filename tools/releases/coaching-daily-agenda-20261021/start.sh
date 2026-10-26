set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-daily-agenda-20261021
echo '1215ea7c7ac3550e51ab0b72525701eb68cf8599d0466702b9aa661bda120f18  /var/lib/eduivme/releases/coaching-daily-agenda-20261021.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Daily agenda isolated build started.'

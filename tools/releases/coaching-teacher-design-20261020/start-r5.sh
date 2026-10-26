set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r5
echo '32f102cd53110de37b79883db49633360273f2127e756879533bb3a3bbfa15b2  /var/lib/eduivme/releases/coaching-teacher-design-20261020-r5.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Teacher design isolated build started.'

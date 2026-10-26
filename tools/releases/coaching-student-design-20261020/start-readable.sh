set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r6
echo '41b85026773a76d1499da31790a7878a587b9d5f3a58cf4a61eb9940adb99cd1  /var/lib/eduivme/releases/coaching-student-design-20261020-r6.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Final student design build started.'

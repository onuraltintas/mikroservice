set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r5
echo '86fe65b0e68436faf29c188f7353fa49a1a04b96e1f7a558d711b5986cb7f8a1  /var/lib/eduivme/releases/coaching-student-design-20261020-r5.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Final student design build started.'

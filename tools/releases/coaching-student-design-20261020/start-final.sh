set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r3
echo '055fb95e3ee8d0d34233a2a178b755085238a94bb0be74dc7531811afcf5f57b  /var/lib/eduivme/releases/coaching-student-design-20261020-r3.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Final student design build started.'

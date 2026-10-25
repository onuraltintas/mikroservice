set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r4
echo 'f317c58b30ed5938cb4dd71c61b4fd0231bd33879a8122952445238a1963ef65  /var/lib/eduivme/releases/coaching-student-design-20261020-r4.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Final student design build started.'

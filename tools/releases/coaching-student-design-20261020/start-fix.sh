set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r2
echo '4fb9e44a48e0e226886ced58802a6ba191943f87cd43a67935f7f8582af0d9f9  /var/lib/eduivme/releases/coaching-student-design-20261020-r2.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Calendar contrast fix isolated build started.'

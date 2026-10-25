set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r4
echo 'f4faa9a0c27bfef4337f3614ba2b11e764a883b3bd435b94aeb24859fb7f3c55  /var/lib/eduivme/releases/coaching-teacher-design-20261020-r4.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Teacher design isolated build started.'

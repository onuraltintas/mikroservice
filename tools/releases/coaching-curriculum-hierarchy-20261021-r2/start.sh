set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021-r2
echo '27cde296b65d3937a7df613a895a733ee13f97eefc5d589724c53d337fac4613  /var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021-r2.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Curriculum hierarchy isolated build started.'

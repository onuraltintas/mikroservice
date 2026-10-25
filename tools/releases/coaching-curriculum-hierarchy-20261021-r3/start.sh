set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021-r3
echo 'e544238e2492cba14846212693b752cc22bb40995bb7fa7f98d166672d13da3c  /var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021-r3.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Curriculum hierarchy isolated build started.'

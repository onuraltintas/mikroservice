set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021
echo 'b6c5722a172447bd18496e51c7ae9ae148d2fd82ae818335fc3a5593766838f2  /var/lib/eduivme/releases/coaching-curriculum-hierarchy-20261021.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Curriculum hierarchy isolated build started.'

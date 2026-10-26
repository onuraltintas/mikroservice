set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-decision-work-20261020
echo '4c3b0e2bef9dfddbde12c32c1f520b54d31df239079632149de031ff93b1e41d  /var/lib/eduivme/releases/coaching-decision-work-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"
mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"
bash -n deploy.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Isolated decision-work build started.'

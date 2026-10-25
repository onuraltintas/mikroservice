set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-tracking-20261020
archive=/var/lib/eduivme/releases/coaching-tracking-20261020.tar.gz
echo '022b67253b2e9ae1fcbf74cad10d23ecef6ec03430b8798208384c969b685cb5  /var/lib/eduivme/releases/coaching-tracking-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$archive" -C "$release"
cd "$release"; bash -n deploy.sh; bash -n preservation.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Backup and preparation complete; isolated portal/image build started.'

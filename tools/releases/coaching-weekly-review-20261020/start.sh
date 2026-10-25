set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-review-20261020
echo 'cf5ae6fc8045b632c195a17b86b67bd20683e4a58e4e2317fc8e5f5ba449e3d5  /var/lib/eduivme/releases/coaching-weekly-review-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf /var/lib/eduivme/releases/coaching-weekly-review-20261020.tar.gz -C "$release"
cd "$release"; bash -n deploy.sh; bash -n history.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Backups and preservation baseline complete; isolated build started.'

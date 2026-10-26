set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-followup-20261020
echo '701fea816597b2dda20a2773c6b840a4249313378b976de111d0ad24631f2298  /var/lib/eduivme/releases/coaching-weekly-followup-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash -n history.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Backups and preservation baseline complete; isolated build started.'

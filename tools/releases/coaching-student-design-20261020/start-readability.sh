set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r7
echo '9ed21f88b5bf7a99577cbc9a2da82c6cc8cf6c684644440c1c39e844d8bfc57c  /var/lib/eduivme/releases/coaching-student-design-20261020-r7.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Calendar contrast fix isolated build started.'

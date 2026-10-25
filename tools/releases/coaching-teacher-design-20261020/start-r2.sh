set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r2
echo '37811b8caa3b87dbc99cd9b6cb66534ef43fd2d728ad9512609735cf00996970  /var/lib/eduivme/releases/coaching-teacher-design-20261020-r2.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Teacher design isolated build started.'

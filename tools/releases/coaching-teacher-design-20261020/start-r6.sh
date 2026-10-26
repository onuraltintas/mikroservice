set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r6
echo '8b5af3371e3aed2ff3c159eb928c65425aa743c43b238f36cb67615ce17aca86  /var/lib/eduivme/releases/coaching-teacher-design-20261020-r6.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Teacher design isolated build started.'

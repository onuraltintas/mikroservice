set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020
echo '8c8900cc77c9d752c18ec4cbd4e5ec5be99ca953b48ff516cecf836b6d6da71e  /var/lib/eduivme/releases/coaching-student-design-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash -n history.sh; bash -n public-check.sh
bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Frontend-only source prepared on server; isolated build started. Database schemas and backend images are unchanged.'

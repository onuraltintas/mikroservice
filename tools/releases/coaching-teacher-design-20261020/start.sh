set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020
echo '91ac2bec9b25a77eaa4f2dd7e689bae1aba25f6636d6e61b63d529ff4e7fed62  /var/lib/eduivme/releases/coaching-teacher-design-20261020.tar.gz' | sha256sum -c -
test ! -e "$release"; mkdir -m 700 "$release"
tar -xzf "$release.tar.gz" -C "$release"
cd "$release"; bash -n deploy.sh; bash deploy.sh prepare
nohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &
echo 'Teacher design isolated build started.'

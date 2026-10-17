set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' ddc2b03adbbd11c1d340f6fd8516a5c8cf8b7c10caa2d647c7043b5d8bd7509a release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

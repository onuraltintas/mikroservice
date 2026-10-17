set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-yearly-calendar-20261017
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' 421ab15e54255f7dbfa48b60c34a7c63f07d434355c8ca45b0ec6f8497de9329 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

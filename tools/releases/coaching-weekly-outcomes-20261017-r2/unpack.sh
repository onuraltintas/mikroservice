set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' 0d5957750cb01941126aabe9cfc85cf833f72b7818e9f8fad1eb06ef812fdca4 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' 6ec0e9703a76db7a91becf57543c4c9d313356230dc7fecd18defd2d27edd3c9 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
bash deploy.sh prepare
bash deploy.sh build
bash deploy.sh drill

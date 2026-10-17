set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' 8c54f2e63917e52960455a93c618ad55f70dfe049ff50aef8ed3ee6d84f27030 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
bash deploy.sh prepare
bash deploy.sh build
bash deploy.sh drill

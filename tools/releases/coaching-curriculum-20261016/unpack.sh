set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-curriculum-20261016
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' cce88e94af08c8797f69470a18cf1fde6e62c19e674a239301f23e5019720765 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

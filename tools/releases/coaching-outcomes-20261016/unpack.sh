set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-outcomes-20261016
test ! -e prepared.txt
printf '%s  %s\n' de34b9f50bc09c030197f07170141da1103e23ae16ece61f7a11279a4c550ae9 release-r3.tar.gz | sha256sum -c -
test ! -e payload.sha256
tar -xzf release-r3.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Payload and deployment syntax verified.'

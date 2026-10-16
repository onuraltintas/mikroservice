set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-exam-date-20261016
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' c207e754d5f48d55dc8d63a46870aad08eea9c6dcd14e31cd806759283d37f4d release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

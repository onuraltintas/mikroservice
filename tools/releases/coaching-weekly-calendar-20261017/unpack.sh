set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-weekly-calendar-20261017
test ! -e prepared.txt; test ! -e payload.sha256
printf '%s  %s\n' 8e3f89873abcb2b397507040f98ced28fd1e4312c50a65c62b97668347aab612 release.tar.gz | sha256sum -c -
tar -xzf release.tar.gz
sha256sum -c payload.sha256 > payload-verified.log
bash -n deploy.sh
echo 'Archive, payload and deployment syntax verified.'

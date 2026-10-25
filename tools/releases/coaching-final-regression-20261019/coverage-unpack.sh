set -euo pipefail
umask 077
archive=/tmp/coaching-wizard-coverage-20261019.tar.gz
echo '4a4a15b8c2886f1e8fc52d167488f90b49d64bc1c5c5245b9040d6d4de2d349a  /tmp/coaching-wizard-coverage-20261019.tar.gz' | sha256sum -c -
release=/var/lib/eduivme/releases/coaching-wizard-coverage-20261019
test ! -e "$release"
mkdir "$release"
tar -xzf "$archive" -C "$release"
cd "$release"
sha256sum -c payload.sha256 > unpack-verified.log

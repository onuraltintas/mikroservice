set -euo pipefail
umask 077
archive=/tmp/coaching-yks-20261019-r3.tar.gz
release=/var/lib/eduivme/releases/coaching-yks-20261019-r3
echo 'f2a134e5f6f5410318f8691657b9c18038e48ecad99cbd188bf38488782cdabe  /tmp/coaching-yks-20261019-r3.tar.gz'|sha256sum -c -
test ! -e "$release"
mkdir -p "$release"
tar -xzf "$archive" -C "$release"
cd "$release"
sha256sum -c payload.sha256 > unpack-verified.log
echo 'Reviewed YKS payload unpacked and checksums verified.'

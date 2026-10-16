set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-curriculum-20261016
test ! -e "$release"
mkdir -p "$release"
echo 'Private release directory created.'

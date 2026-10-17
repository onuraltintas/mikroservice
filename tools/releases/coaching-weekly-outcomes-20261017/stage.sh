set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-outcomes-20261017
test ! -e "$release"
mkdir -p "$release"
echo 'Private release directory created.'

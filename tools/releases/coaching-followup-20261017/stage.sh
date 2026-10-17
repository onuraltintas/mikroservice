set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-followup-20261017
test ! -e "$release"
mkdir -p "$release"
echo 'Private release directory created.'

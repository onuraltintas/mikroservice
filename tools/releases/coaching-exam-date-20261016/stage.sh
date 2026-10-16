set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-exam-date-20261016
test ! -e "$release"
mkdir -p "$release"
echo 'Private release directory created.'

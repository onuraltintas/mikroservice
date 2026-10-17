set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-calendar-20261017
test ! -e "$release"
mkdir -p "$release"
echo 'Private release directory created.'

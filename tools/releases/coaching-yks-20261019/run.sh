set -euo pipefail
case "${1:-}" in prepare|build|drill|promote|verify) ;; *)exit 2;;esac
exec bash /var/lib/eduivme/releases/coaching-yks-20261019-r3/deploy.sh "$1"

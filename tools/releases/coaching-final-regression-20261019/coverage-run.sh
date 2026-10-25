set -euo pipefail
case "${1:-}" in prepare|build|promote|verify);;*)exit 2;;esac
exec bash /var/lib/eduivme/releases/coaching-wizard-coverage-20261019/deploy.sh "$1"

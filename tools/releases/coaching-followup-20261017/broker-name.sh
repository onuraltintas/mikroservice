set -euo pipefail
docker ps --format '{{.Names}}' | grep -E 'rabbit|postgres'

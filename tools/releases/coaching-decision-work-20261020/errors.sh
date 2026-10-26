set -euo pipefail
docker logs --since 8m --tail 120 eduivme-production-coaching-service-1 2>&1 | tail -n 90

set -euo pipefail
cd /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2
bash deploy.sh verify
docker inspect eduivme-production-staff-portal-1 --format 'staff health={{.State.Health.Status}} restarts={{.RestartCount}}'
test "$(docker inspect eduivme-production-staff-portal-1 --format '{{.State.Health.Status}}')" = healthy
bash public-check.sh

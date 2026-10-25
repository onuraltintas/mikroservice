set -euo pipefail
cd /var/lib/eduivme/releases/coaching-teacher-design-20261020-r6
bash deploy.sh verify
docker inspect eduivme-production-staff-portal-1 --format 'health={{.State.Health.Status}} restarts={{.RestartCount}} image={{.Config.Image}}'
test "$(docker inspect eduivme-production-staff-portal-1 --format '{{.State.Health.Status}}')" = healthy
bash public-check.sh

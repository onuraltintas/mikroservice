set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021
mkdir -m 700 live-contracts
docker cp eduivme-production-coaching-service-1:/app/EduPlatform.Shared.Contracts.dll live-contracts/
docker cp eduivme-production-coaching-service-1:/app/EduPlatform.Shared.Contracts.pdb live-contracts/

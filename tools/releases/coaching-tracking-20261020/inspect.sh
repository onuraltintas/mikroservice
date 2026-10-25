set -euo pipefail
for service in admin-panel staff-portal coaching-service; do
 docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'
done
df -h /var/lib/eduivme
find /var/lib/eduivme/releases/coaching-practice-adaptation-20261020 -maxdepth 3 -type d \( -name services -o -name shared -o -name source -o -name obj \) -print
find /var/lib/eduivme/releases/coaching-practice-yks-regression-20261020 -maxdepth 3 -type d \( -name services -o -name shared -o -name source -o -name obj \) -print
find /var/lib/eduivme/releases/coaching-development-20261020/source -maxdepth 2 -type d -name node_modules -print
find /var/lib/eduivme/releases/coaching-practice-adaptation-20261020 -maxdepth 2 -type f -name '*.Dockerfile' -print
docker images --format '{{.Repository}}:{{.Tag}}' | grep -E 'sdk|aspnet|node' | head -15

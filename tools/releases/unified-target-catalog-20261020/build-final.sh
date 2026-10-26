set -euo pipefail
cd /var/lib/eduivme/releases/unified-target-catalog-20261020-build
mkdir -p clients-shared
tar -xzf clients-shared.tar.gz -C clients-shared
docker run --rm --name unified-target-portal-builder-20261020 --memory=4g --cpus=2 \
 -v "$PWD/portal-source:/work/admin-panel" -v "$PWD/clients-shared/shared:/work/shared:ro" \
 -w /work/admin-panel node:24-alpine node build-portals.cjs > build-final.log 2>&1
find portal-source/dist -maxdepth 3 -name 'index*html'

set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/unified-target-catalog-20261020-build
mkdir portal-source
tar -xzf portal-source.tar.gz -C portal-source
cp build-portals.cjs portal-source/build-portals.cjs
docker run --rm --name unified-target-portal-builder-20261020 --memory=4g --cpus=2 \
  -v "$PWD/portal-source:/work" -w /work node:24-alpine \
  sh -c 'npm ci --no-audit --no-fund && node build-portals.cjs' > build.log 2>&1
find portal-source/dist -maxdepth 3 -name 'index*html'

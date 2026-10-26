set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-practice-adaptation-20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
test -s deployed.txt; test ! -e hotfix-deployed.txt
sha256sum -c hotfix.sha256
tar -xzf hotfix.tar.gz
sha256sum -c hotfix-source.sha256
docker ps --format '{{.Names}} {{.ID}}' --no-trunc > hotfix-containers.before.txt
for service in admin-panel staff-portal; do
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
done
docker run --rm --name coaching-practice-hotfix-builder-20261020 --memory=4g --cpus=2 --network none \
  -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > hotfix-portals-build.log 2>&1
cp -a source/admin-panel/dist/admin-panel/. payload/admin/
cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
tag=coaching-practice-adaptation-20261020-r2
for service in admin-panel staff-portal; do
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:coaching-practice-adaptation-20261020" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "hotfix-$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "hotfix-$service-image.txt"
done
jq --arg tag "$tag" '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' production.compose.json > hotfix.compose.json
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
"${compose[@]}" -f hotfix.compose.json config --quiet
recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f production.compose.json up -d --no-deps --pull never admin-panel staff-portal >> hotfix-recovery.log 2>&1 || true; fi; exit "$status"; }
trap recover EXIT
for service in admin-panel staff-portal; do
  "${compose[@]}" -f hotfix.compose.json up -d --no-deps --pull never "$service" > "hotfix-$service-promote.log" 2>&1
  port=4000; path=/; [[ "$service" == staff-portal ]] && { port=80; path=/staff/; }
  success=0
  for attempt in $(seq 1 30); do
    ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then success=1; break; fi
    sleep 2
  done
  [[ "$success" == 1 ]]
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"hotfix-$service-image.txt")" ]]
done
while read -r name id; do
  case "$name" in eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
done < hotfix-containers.before.txt
date -u +%FT%TZ > hotfix-deployed.txt
trap - EXIT
echo 'Hotfix deployed to both portals; API, database and other containers unchanged.'

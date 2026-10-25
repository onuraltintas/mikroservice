set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-tracking-20261020
base=/var/lib/eduivme/releases/coaching-development-20261020
tag=coaching-tracking-20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(coaching-service admin-panel staff-portal)
stable(){ while read -r name id; do case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac; [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]; done < containers.before.txt; }
baseline(){ for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]]; done; }
ready(){
 local service=$1 port=4000 path=/ ip
 [[ "$service" == staff-portal ]] && { port=80; path=/staff/; }
 [[ "$service" == coaching-service ]] && { port=8080; path=/health/ready; }
 for attempt in $(seq 1 30); do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
  sleep 2
 done
 return 1
}
case "${1:-}" in
prepare)
 test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 cp "$base/api-production.compose.json" rollback.compose.json
 for service in "${services[@]}"; do
  docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
  image=$(<"previous-$service-image.txt")
  jq --arg s "$service" --arg i "$image" '.services[$s].image=$i|.services[$s].pull_policy="never"' rollback.compose.json > next.compose.json; mv next.compose.json rollback.compose.json
 done
 expected=$(cat api-baseline.sha256)
 [[ $(docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.API.dll | cut -d' ' -f1) == "$expected" ]]
 jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 db_user=$(docker exec postgres printenv POSTGRES_USER)
 for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; done
 sha256sum *.before.backup > backups.sha256; bash preservation.sh before
 mkdir -p source
 cp -a "$base/source/admin-panel" source/admin-panel
 cp -a "$base/source/shared" source/shared
 for file in $(find overlay -type f | sort); do old="source/admin-panel/${file#overlay/}"; if test -f "$old"; then diff -u "$old" "$file" >> source-overlay.diff || [[ $? == 1 ]]; fi; done
 cp -a overlay/. source/admin-panel/; cp build-portals.cjs source/admin-panel/
 date -u +%FT%TZ > prepared.txt
 echo 'Three DB backups, exact API baseline and isolated frontend source are ready.'
 ;;
build)
 test -s prepared.txt; test ! -e built.txt; baseline
 docker run --rm --name coaching-tracking-portal-builder-20261020 --memory=4g --cpus=2 --network none -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > portals-build.log 2>&1
 mkdir -p payload/admin payload/staff
 cp -a source/admin-panel/dist/admin-panel/. payload/admin/
 cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
 test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
 for service in "${services[@]}"; do
  cp "/var/lib/eduivme/releases/coaching-practice-adaptation-20261020/$service.Dockerfile" "$service.Dockerfile"
  docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
 done
 find payload -type f | LC_ALL=C sort | xargs sha256sum > runtime.sha256
 stable; date -u +%FT%TZ > built.txt
 echo 'API and two portals built; dependencies and database schema unchanged.'
 ;;
promote)
 test -s built.txt; test -s drill-verified.txt; test ! -e deployed.txt; baseline; stable
 sha256sum -c runtime.sha256 > runtime-verified.log
 bash preservation.sh after; bash preservation.sh compare
 recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" >> rollback.log 2>&1 || true; fi; exit "$status"; }
 trap recover EXIT
 for service in "${services[@]}"; do
  "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1
  ready "$service"
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
 done
 stable; bash preservation.sh after; bash preservation.sh compare
 date -u +%FT%TZ > deployed.txt; trap - EXIT
 echo 'Tracking API and both portals deployed; every protected row is unchanged.'
 ;;
verify)
 test -s deployed.txt
 for service in "${services[@]}"; do ready "$service"; docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'; done
 stable
 ;;
*) exit 2;;
esac

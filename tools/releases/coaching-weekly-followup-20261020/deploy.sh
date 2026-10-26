set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-followup-20261020
base=/var/lib/eduivme/releases/coaching-weekly-review-20261020
tag=coaching-weekly-followup-20261020
drill=coaching_weekly_followup_probe_20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(notification-service coaching-service admin-panel staff-portal)
db_user=$(docker exec postgres printenv POSTGRES_USER)
stable(){ while read -r name id; do case "$name" in eduivme-production-notification-service-1|eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac; [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]; done < containers.before.txt; }
baseline(){ for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]]; done; }
ready(){
 local service=$1 port=8080 path=/health/ready ip
 [[ "$service" != admin-panel ]] || { port=4000; path=/; }
 [[ "$service" != staff-portal ]] || { port=80; path=/staff/; }
 for attempt in $(seq 1 30); do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
  sleep 2
 done
 return 1
}
queues(){
 for attempt in $(seq 1 20); do
  docker exec rabbitmq rabbitmqctl -q list_queues name consumers messages_ready messages_unacknowledged --formatter json > queues.json
  if jq -e '[.[] | select((.name=="coaching-weekly-reflection-saved" or .name=="coaching-weekly-decision-changed") and .consumers>=1)] | length==2' queues.json >/dev/null; then return 0; fi
  sleep 2
 done
 echo 'Weekly notification consumers are not ready.' >&2; return 1
}
schema(){ docker exec postgres pg_dump -U "$db_user" -d "$1" --schema-only --no-owner --no-privileges | sed '/^\\restrict /d;/^\\unrestrict /d;/^--/d;/^$/d' > "$2"; }
case "${1:-}" in
prepare)
 test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
 for prefix in Coaching Notification; do
  service=coaching-service; [[ "$prefix" != Notification ]] || service=notification-service
  while read -r expected file; do [[ $(docker exec "eduivme-production-$service-1" sha256sum "$file" | cut -d' ' -f1) == "$expected" ]]; done < "baseline-$prefix.sha256"
 done
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1 > network.txt
 cp "$base/production.compose.json" rollback.compose.json
 for service in "${services[@]}"; do
  docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
  jq --arg s "$service" --arg i "$(<"previous-$service-image.txt")" '.services[$s].image=$i|.services[$s].pull_policy="never"' rollback.compose.json > next.compose.json; mv next.compose.json rollback.compose.json
 done
 jq --arg tag "$tag" '.services["notification-service"].image=("eduivme/notification-service:"+$tag)|.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; schema "$database" "$database.schema.before.txt"; done
 sha256sum *.before.backup > backups.sha256; bash history.sh before
 mkdir source; cp -a "$base/source/admin-panel" source/admin-panel; cp -a "$base/source/shared" source/shared
 for file in $(find overlay -type f | sort); do old="source/admin-panel/${file#overlay/}"; if test -f "$old"; then diff -u "$old" "$file" >> source-overlay.diff || [[ $? == 1 ]]; fi; done
 cp -a overlay/. source/admin-panel/; cp build-portals.cjs source/admin-panel/
 date -u +%FT%TZ > prepared.txt; echo 'Live module baseline, backups, schema and original rows captured.'
 ;;
build)
 test -s prepared.txt; test ! -e built.txt; baseline
 docker run --rm --name coaching-weekly-followup-builder-20261020 --memory=4g --cpus=2 --network none -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > portals-build.log 2>&1
 mkdir -p payload/admin payload/staff; cp -a source/admin-panel/dist/admin-panel/. payload/admin/; cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
 test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
 for service in "${services[@]}"; do
  if [[ "$service" == admin-panel || "$service" == staff-portal ]]; then cp "/var/lib/eduivme/releases/coaching-practice-adaptation-20261020/$service.Dockerfile" "$service.Dockerfile"; fi
  docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
 done
 find payload -type f | LC_ALL=C sort | xargs sha256sum > runtime.sha256
 stable; date -u +%FT%TZ > built.txt; echo 'Four isolated images built.'
 ;;
drill)
 test -s built.txt; test ! -e drill-verified.txt; baseline
 connection=$(docker inspect eduivme-production-coaching-service-1 --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
 pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
 docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
 cleanup(){ rm -f probe.env; docker exec postgres dropdb -U "$db_user" "$drill"; }; trap cleanup EXIT
 docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
 schema "$drill" drill.schema.before.txt
 probe_connection=$(jq -nr --arg c "$connection" --arg db "$drill" '$c|sub("Database=coaching_db;";"Database="+$db+";")'); printf 'WEEKLY_CONNECTION=%s\n' "$probe_connection" > probe.env
 docker run --rm --memory=512m --cpus=1 --network "$(<network.txt)" --env-file probe.env -v "$release:/fix:ro" --entrypoint /bin/sh "eduivme/coaching-service:$tag" -c 'cp -a /app /tmp/probe; cp /fix/Verification.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Verification.dll probe' > drill-probe.log 2>&1
 schema "$drill" drill.schema.after.txt; cmp drill.schema.before.txt drill.schema.after.txt; cat drill-probe.log
 stable; date -u +%FT%TZ > drill-verified.txt
 ;;
promote)
 test -s drill-verified.txt; test ! -e deployed.txt; baseline; stable; sha256sum -c runtime.sha256 > runtime-verified.log
 recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" >> rollback.log 2>&1 || true; fi; exit "$status"; }; trap recover EXIT
 date -u +%FT%TZ > promotion-started.txt
 for service in "${services[@]}"; do
  "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1; ready "$service"
  [[ "$service" != notification-service ]] || queues
 done
 stable; bash history.sh after; bash history.sh compare
 for database in coaching_db identity_db notification_db; do schema "$database" "$database.schema.after.txt"; cmp "$database.schema.before.txt" "$database.schema.after.txt"; done
 date -u +%FT%TZ > deployed.txt; trap - EXIT; echo 'Four services promoted, notification consumers ready, every original row and schema unchanged.'
 ;;
verify)
 test -s deployed.txt
 for service in "${services[@]}"; do ready "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; done
 queues; stable; echo 'Readiness, exact images, notification consumers and unrelated containers verified.'
 ;;
*) exit 2;;
esac

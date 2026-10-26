set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-weekly-review-20261020
base=/var/lib/eduivme/releases/coaching-tracking-20261020
tag=coaching-weekly-review-20261020
drill=coaching_weekly_probe_20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(coaching-service admin-panel staff-portal)
db_user=$(docker exec postgres printenv POSTGRES_USER)
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
protected(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort > "$2"
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',
 table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens','__ef_migrations_history','__EFMigrationsHistory','weekly_reflections','weekly_decisions') ORDER BY table_name \gexec
COMMIT;
SQL
}
migrate(){
 local database=$1 connection
 [[ "$database" == coaching_db || "$database" == "$drill" ]]
 connection=$(docker inspect eduivme-production-coaching-service-1 --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
 [[ "$connection" == *'Database=coaching_db;'* ]]
 if [[ "$database" != coaching_db ]]; then connection=$(jq -nr --arg c "$connection" --arg db "$database" '$c|sub("Database=coaching_db;";"Database="+$db+";")'); fi
 printf 'ConnectionStrings__DefaultConnection=%s\n' "$connection" > migrate.env
 docker run --rm --memory=512m --cpus=1 --network "$(<network.txt)" --entrypoint dotnet --env-file migrate.env -e ASPNETCORE_ENVIRONMENT=Production "eduivme/coaching-service:$tag" Coaching.API.dll --migrate-only > "$database-migrate.log" 2>&1
 rm -f migrate.env
}
case "${1:-}" in
prepare)
 test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 1') == '20261020100033_PracticeTopicAdaptation' ]]
 while read -r expected file; do [[ $(docker exec eduivme-production-coaching-service-1 sha256sum "$file" | cut -d' ' -f1) == "$expected" ]]; done < baseline.sha256
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1 > network.txt
 cp "$base/production.compose.json" rollback.compose.json
 for service in "${services[@]}"; do
  docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
  jq --arg s "$service" --arg i "$(<"previous-$service-image.txt")" '.services[$s].image=$i|.services[$s].pull_policy="never"' rollback.compose.json > next.compose.json; mv next.compose.json rollback.compose.json
 done
 jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; protected "$database" "$database.protected.before.txt"; done
 sha256sum *.before.backup > backups.sha256; bash history.sh before
 mkdir source; cp -a "$base/source/admin-panel" source/admin-panel; cp -a "$base/source/shared" source/shared
 for file in $(find overlay -type f | sort); do old="source/admin-panel/${file#overlay/}"; if test -f "$old"; then diff -u "$old" "$file" >> source-overlay.diff || [[ $? == 1 ]]; fi; done
 cp -a overlay/. source/admin-panel/; cp build-portals.cjs source/admin-panel/
 date -u +%FT%TZ > prepared.txt
 echo 'Four live modules verified; three DB backups and exact frontend source ready.'
 ;;
build)
 test -s prepared.txt; test ! -e built.txt; baseline
 docker run --rm --name coaching-weekly-portal-builder-20261020 --memory=4g --cpus=2 --network none -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > portals-build.log 2>&1
 mkdir -p payload/admin payload/staff; cp -a source/admin-panel/dist/admin-panel/. payload/admin/; cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
 test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
 for service in "${services[@]}"; do
  cp "/var/lib/eduivme/releases/coaching-practice-adaptation-20261020/$service.Dockerfile" "$service.Dockerfile"
  docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
 done
 find payload -type f | LC_ALL=C sort | xargs sha256sum > runtime.sha256
 stable; date -u +%FT%TZ > built.txt; echo 'Four module overlay and two isolated portal images built.'
 ;;
drill)
 test -s built.txt; test ! -e drill-verified.txt; baseline; sha256sum -c runtime.sha256 > drill-runtime.log
 connection=$(docker inspect eduivme-production-coaching-service-1 --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
 pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
 docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
 cleanup(){ rm -f migrate.env probe.env; docker exec postgres dropdb -U "$db_user" "$drill"; }; trap cleanup EXIT
 docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
 protected "$drill" drill.before.txt; migrate "$drill"
 probe_connection=$(jq -nr --arg c "$connection" --arg db "$drill" '$c|sub("Database=coaching_db;";"Database="+$db+";")'); printf 'WEEKLY_CONNECTION=%s\n' "$probe_connection" > probe.env
 docker run --rm --memory=512m --cpus=1 --network "$(<network.txt)" --env-file probe.env -v "$release:/fix:ro" --entrypoint /bin/sh "eduivme/coaching-service:$tag" -c 'cp -a /app /tmp/probe; cp /fix/Verification.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Verification.dll probe' > drill-probe.log 2>&1
 protected "$drill" drill.after.txt; cmp drill.before.txt drill.after.txt; cat drill-probe.log
 stable; date -u +%FT%TZ > drill-verified.txt
 echo 'Migration and complete weekly workflow passed on restored live backup; all old tables match.'
 ;;
promote)
 test -s drill-verified.txt; test ! -e deployed.txt; baseline; stable
 sha256sum -c runtime.sha256 > runtime-verified.log
 recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" >> rollback.log 2>&1 || true; fi; exit "$status"; }; trap recover EXIT
 "${compose[@]}" -f rollback.compose.json stop coaching-service > stop.log 2>&1
 docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-promotion.backup
 protected coaching_db promotion.before.txt; migrate coaching_db; protected coaching_db promotion.after.txt; cmp promotion.before.txt promotion.after.txt
 for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1; ready "$service"; done
 stable; bash history.sh after; bash history.sh compare
 date -u +%FT%TZ > deployed.txt; trap - EXIT
 echo 'Coaching API and both portals deployed; all original rows preserved.'
 ;;
verify)
 test -s deployed.txt
 for service in "${services[@]}"; do ready "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; done
 stable
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 1') == '20261020130142_WeeklyReviewsAndDecisions' ]]
 echo 'Readiness, exact images, migration and unrelated containers verified.'
 ;;
*) exit 2;;
esac

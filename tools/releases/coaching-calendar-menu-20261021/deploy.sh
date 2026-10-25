set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-menu-20261021
base=/var/lib/eduivme/releases/coaching-decision-work-20261020
tag=coaching-calendar-menu-20261021
drill=coaching_calendar_menu_probe_20261021
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(coaching-service admin-panel staff-portal)
db_user=$(docker exec postgres printenv POSTGRES_USER)
baseline(){ for s in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$(<"previous-$s.txt")" ]]; done; }
stable(){ while read -r name id; do case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac; [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]; done < containers.before.txt; }
ready(){ local s=$1 port=8080 path=/health/ready ip; [[ "$s" != admin-panel ]] || { port=4000; path=/; }; [[ "$s" != staff-portal ]] || { port=80; path=/staff/; }; for i in $(seq 1 30); do ip=$(docker inspect "eduivme-production-$s-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1); if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return; fi; sleep 2; done; return 1; }
schema(){ docker exec postgres pg_dump -U "$db_user" -d "$1" --schema-only --no-owner --no-privileges | sed '/^\\restrict /d;/^\\unrestrict /d;/^--/d;/^$/d' > "$2"; }
case "${1:-}" in
prepare)
 test ! -e prepared.txt; sha256sum -c payload.sha256 > package-verified.log
 while read -r hash file; do [[ $(docker exec eduivme-production-coaching-service-1 sha256sum "$file" | cut -d' ' -f1) == "$hash" ]]; done < baseline-coaching.sha256
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$c:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1 > network.txt
 cp "$base/final.compose.json" rollback.compose.json
 for s in "${services[@]}"; do docker inspect "eduivme-production-$s-1" --format '{{.Image}}' > "previous-$s.txt"; jq --arg s "$s" --arg i "$(<"previous-$s.txt")" '.services[$s].image=$i|.services[$s].pull_policy="never"' rollback.compose.json > next.json; mv next.json rollback.compose.json; done
 jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; schema "$database" "$database.schema.before.txt"; done
 sha256sum *.before.backup > backups.sha256; bash history.sh before
 docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "Version","UpdatedAt" FROM coaching.study_plan_revisions WHERE "Id"='"'a4cea155-5485-4db6-9f52-1ec4df8080c3'" > plan-metadata.before.txt
 mkdir source; cp -a "$base/source/admin-panel" source/admin-panel; cp -a "$base/source/shared" source/shared
 for file in $(find overlay -type f | sort); do old="source/admin-panel/${file#overlay/}"; if test -f "$old"; then diff -u "$old" "$file" >> source-overlay.diff || [[ $? == 1 ]]; fi; done
 cp -a overlay/. source/admin-panel/; cp build-portals.cjs source/admin-panel/
 date -u +%FT%TZ > prepared.txt; echo 'Three backups and original rows captured; nine scoped portal files overlaid.';;
build)
 test -s prepared.txt; test ! -e built.txt; baseline
 docker run --rm --name coaching-calendar-menu-builder-20261021 --memory=4g --cpus=2 --network none -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > portals-build.log 2>&1
 mkdir -p payload/admin payload/staff; cp -a source/admin-panel/dist/admin-panel/. payload/admin/; cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
 test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
 for s in "${services[@]}"; do
  if [[ "$s" != coaching-service ]]; then docker image inspect "$(<"previous-$s.txt")" > runtime-image.json; docker run --rm --memory=1g --network none -v "$release:/release" -w /release node:24-alpine node "runtime-$s.cjs" > "runtime-$s.log"; fi
  docker tag "$(<"previous-$s.txt")" "eduivme/$s:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$s:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$s.Dockerfile" -t "eduivme/$s:$tag" . > "$s-build.log" 2>&1
  docker image inspect "eduivme/$s:$tag" --format '{{.Id}}' > "built-$s.txt"
 done
 stable; date -u +%FT%TZ > built.txt; echo 'Three isolated images built.';;
drill)
 test -s built.txt; test ! -e drill-verified.txt; baseline
 connection=$(docker inspect eduivme-production-coaching-service-1 --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
 pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
 docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
 cleanup(){ rm -f probe.env; docker exec postgres dropdb -U "$db_user" "$drill"; }; trap cleanup EXIT
 docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
 probe_connection=$(jq -nr --arg c "$connection" --arg db "$drill" '$c|sub("Database=coaching_db;";"Database="+$db+";")'); printf 'WEEKLY_CONNECTION=%s\n' "$probe_connection" > probe.env
 docker run --rm --memory=1g --cpus=1 --network "$(<network.txt)" --env-file probe.env -v "$release:/fix:ro" --entrypoint /bin/sh "eduivme/coaching-service:$tag" -c 'cp -a /app /tmp/probe; cp /fix/Verification.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Verification.dll probe' > drill-probe.log 2>&1
 cat drill-probe.log; stable; date -u +%FT%TZ > drill-verified.txt;;
promote)
 test -s drill-verified.txt; test ! -e deployed.txt; baseline; stable
 recover(){ local code=$?; trap - EXIT; if ((code!=0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || true; fi; exit "$code"; }; trap recover EXIT
 date -u +%FT%TZ > promotion-start.txt
 for s in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$s" > "$s-promote.log" 2>&1; ready "$s"; done
 stable; bash history.sh after; bash history.sh compare
 for database in coaching_db identity_db notification_db; do schema "$database" "$database.schema.after.txt"; cmp "$database.schema.before.txt" "$database.schema.after.txt"; done
 docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "Version","UpdatedAt" FROM coaching.study_plan_revisions WHERE "Id"='"'a4cea155-5485-4db6-9f52-1ec4df8080c3'" > plan-metadata.promoted.txt
 cmp plan-metadata.before.txt plan-metadata.promoted.txt
 date -u +%FT%TZ > deployed.txt; trap - EXIT; echo 'Three services deployed; schema and all existing business rows preserved.';;
verify)
 test -s deployed.txt
 for s in "${services[@]}"; do ready "$s"; [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$(<"built-$s.txt")" ]]; docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'; done
 stable; bash public-check.sh;;
*) exit 2;;
esac

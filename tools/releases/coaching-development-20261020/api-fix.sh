set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-development-20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
tag=coaching-development-20261020-attachment-fix
container=eduivme-production-coaching-service-1
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
case "${1:-}" in
drill)
 test -s deployed.txt; test ! -e api-drill-verified.txt
 sha256sum -c api-fix-archive.sha256
 mkdir -m 700 api-fix; tar -xzf api-fix.tar.gz -C api-fix
 (cd api-fix; sha256sum -c payload.sha256)
 [[ $(docker inspect "$container" --format '{{.Config.Image}}') == 'eduivme/coaching-service:coaching-practice-adaptation-20261020-r3' ]]
 docker inspect "$container" --format '{{.Image}}' > api-previous-image.txt
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > api-containers.before.txt
 network=$(docker inspect "$container" --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1)
 connection=$(docker inspect "$container" --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
 [[ "$connection" == *'Database=coaching_db;'* ]]
 pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
 drill=coaching_development_probe_20261020
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
 docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
 cleanup(){ rm -f probe.env; docker exec postgres dropdb -U "$db_user" "$drill"; }
 trap cleanup EXIT
 docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > api-drill-restore.log 2>&1
 probe_connection=$(jq -nr --arg c "$connection" --arg db "$drill" '$c|sub("Database=coaching_db;";"Database="+$db+";")')
 [[ "$probe_connection" == *"Database=$drill;"* ]]
 printf 'PROBE_CONNECTION=%s\n' "$probe_connection" > probe.env
 image=$(<api-previous-image.txt)
 if docker run --rm --memory=512m --cpus=1 --network "$network" --env-file probe.env -v "$release/api-fix:/fix:ro" --entrypoint /bin/sh "$image" -c \
   'cp -a /app /tmp/probe; cp /fix/Probe.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Probe.dll' > api-probe-before.log 2>&1; then
  echo 'Expected the original column-mapping failure before applying the patch.' >&2; exit 1
 fi
 grep -q 'column.*CreatedBy.*does not exist' api-probe-before.log
 docker run --rm --memory=512m --cpus=1 --network "$network" --env-file probe.env -v "$release/api-fix:/fix:ro" --entrypoint /bin/sh "$image" -c \
   'cp -a /app /tmp/probe; cp /fix/Probe.dll /tmp/probe/; cp /fix/Coaching.Infrastructure.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Probe.dll' > api-probe-after.log 2>&1
 cat api-probe-after.log
 docker tag "$image" "eduivme/coaching-service:$tag-base"
 printf 'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nCOPY --chmod=644 Coaching.Infrastructure.dll Coaching.Infrastructure.pdb /app/\nLABEL org.opencontainers.image.revision=coaching-development-20261020-attachment-fix\n' > api-fix/Dockerfile
 docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/coaching-service:$tag-base" -t "eduivme/coaching-service:$tag" api-fix > api-fix-build.log 2>&1
 docker image inspect "eduivme/coaching-service:$tag" --format '{{.Id}}' > api-built-image.txt
 date -u +%FT%TZ > api-drill-verified.txt
 echo 'Restored backup reproduces the original failure; corrected mapping reads the same graded submission without schema or data changes.'
 ;;
promote)
 test -s api-drill-verified.txt; test ! -e api-deployed.txt
 [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<api-previous-image.txt)" ]]
 (cd api-fix; sha256sum -c payload.sha256)
 bash preservation.sh after; bash preservation.sh compare
 cp production.compose.json api-rollback.compose.json
 jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)' api-rollback.compose.json > api-production.compose.json
 "${compose[@]}" -f api-production.compose.json config --quiet
 recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f api-rollback.compose.json up -d --no-deps --pull never coaching-service >> api-rollback.log 2>&1 || true; fi; exit "$status"; }
 trap recover EXIT
 "${compose[@]}" -f api-production.compose.json up -d --no-deps --pull never coaching-service > api-promote.log 2>&1
 success=0
 for attempt in $(seq 1 30); do
  ip=$(docker inspect "$container" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null 2>&1; then success=1; break; fi
  sleep 2
 done
 [[ "$success" == 1 ]]
 [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<api-built-image.txt)" ]]
 while read -r name id; do [[ "$name" == "$container" ]] && continue; [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]; done < api-containers.before.txt
 bash preservation.sh after; bash preservation.sh compare
 date -u +%FT%TZ > api-deployed.txt; trap - EXIT
 echo 'Only the corrected Infrastructure assembly deployed. Every protected database row is unchanged.'
 ;;
*) exit 2;;
esac

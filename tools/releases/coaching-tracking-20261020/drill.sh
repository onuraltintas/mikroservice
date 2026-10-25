set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-tracking-20261020
cd "$release"; test -s prepared.txt; test ! -e drill-verified.txt
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
sha256sum -c verification.sha256
db_user=$(docker exec postgres printenv POSTGRES_USER)
container=eduivme-production-coaching-service-1
network=$(docker inspect "$container" --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1)
connection=$(docker inspect "$container" --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
[[ "$connection" == *'Database=coaching_db;'* ]]
pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
drill=coaching_tracking_probe_20261020
[[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
cleanup(){ rm -f probe.env; docker exec postgres dropdb -U "$db_user" "$drill"; }
trap cleanup EXIT
docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
probe_connection=$(jq -nr --arg c "$connection" --arg db "$drill" '$c|sub("Database=coaching_db;";"Database="+$db+";")')
printf 'TRACKING_CONNECTION=%s\n' "$probe_connection" > probe.env
image=$(<previous-coaching-service-image.txt)
docker run --rm --memory=512m --cpus=1 --network "$network" --env-file probe.env -v "$release:/fix:ro" --entrypoint /bin/sh "$image" -c \
 'cp -a /app /tmp/probe; cp /fix/Verification.dll /tmp/probe/; cp /fix/payload/coaching/Coaching.API.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Verification.dll probe' > drill-probe.log 2>&1
cat drill-probe.log
bash preservation.sh after; bash preservation.sh compare
date -u +%FT%TZ > drill-verified.txt
echo 'Tracking queries passed read-only against the restored production backup using the exact live dependencies.'

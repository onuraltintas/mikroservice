set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-tracking-20261020
cd "$release"; test -s deployed.txt
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
case "${1:-}" in seed|cleanup) operation=$1;; *) exit 2;; esac
if [[ "$operation" == seed ]]; then test ! -e fixture-seeded.txt; bash history.sh before; else test -s fixture-seeded.txt; test ! -e fixture-cleaned.txt; fi
sha256sum -c verification.sha256
container=eduivme-production-coaching-service-1
network=$(docker inspect "$container" --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1)
connection=$(docker inspect "$container" --format '{{json .Config.Env}}' | jq -er '.[]|select(startswith("ConnectionStrings__DefaultConnection="))|ltrimstr("ConnectionStrings__DefaultConnection=")')
[[ "$connection" == *'Database=coaching_db;'* ]]
printf 'TRACKING_CONNECTION=%s\n' "$connection" > fixture.env
trap 'rm -f fixture.env' EXIT
docker run --rm --memory=512m --cpus=1 --network "$network" --env-file fixture.env -v "$release:/fix:ro" --entrypoint /bin/sh "$(<built-coaching-service-image.txt)" -c \
 'cp -a /app /tmp/probe; cp /fix/Verification.dll /tmp/probe/; dotnet exec --depsfile /tmp/probe/Coaching.API.deps.json --runtimeconfig /tmp/probe/Coaching.API.runtimeconfig.json /tmp/probe/Verification.dll "$1"' sh "$operation" > "fixture-$operation.log" 2>&1
cat "fixture-$operation.log"
if [[ "$operation" == seed ]]; then date -u +%FT%TZ > fixture-seeded.txt; else date -u +%FT%TZ > fixture-cleaned.txt; fi
bash history.sh after; bash history.sh compare

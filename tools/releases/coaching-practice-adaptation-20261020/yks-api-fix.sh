set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-practice-yks-regression-20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
test -s started.txt; test ! -e api-fixed.txt
sha256sum -c api-fix.sha256
mkdir -p api-fix; tar -xzf api-fix.tar.gz -C api-fix
cd api-fix; sha256sum -c payload.sha256; cd ..
service=eduivme-production-coaching-service-1
[[ $(docker inspect "$service" --format '{{.Config.Image}}') == 'eduivme/coaching-service:coaching-practice-adaptation-20261020' ]]
docker inspect "$service" --format '{{.Image}}' > api-before-image.txt
docker ps --format '{{.Names}} {{.ID}}' --no-trunc > api-fix-containers.before.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-api-fix.backup
docker tag "$(<api-before-image.txt)" eduivme/coaching-service:coaching-practice-adaptation-20261020-r3-base
base=eduivme/coaching-service:coaching-practice-adaptation-20261020-r3-base
printf 'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nCOPY --chmod=644 payload/ /app/\nLABEL org.opencontainers.image.revision=coaching-practice-adaptation-20261020-r3\n' > api-fix/Dockerfile
docker build --network none --pull=false --build-arg "BASE_IMAGE=$base" -t eduivme/coaching-service:coaching-practice-adaptation-20261020-r3 api-fix > api-fix-build.log 2>&1
docker image inspect eduivme/coaching-service:coaching-practice-adaptation-20261020-r3 --format '{{.Id}}' > api-fixed-image.txt
cp /var/lib/eduivme/releases/coaching-practice-adaptation-20261020/hotfix.compose.json api-fix-rollback.compose.json
jq '.services["coaching-service"].image="eduivme/coaching-service:coaching-practice-adaptation-20261020-r3"' api-fix-rollback.compose.json > api-fixed.compose.json
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
"${compose[@]}" -f api-fixed.compose.json config --quiet
recover(){ local status=$?; trap - EXIT; if ((status != 0)); then "${compose[@]}" -f api-fix-rollback.compose.json up -d --no-deps --pull never coaching-service >> api-fix-recovery.log 2>&1 || true; fi; exit "$status"; }
trap recover EXIT
"${compose[@]}" -f api-fixed.compose.json up -d --no-deps --pull never coaching-service > api-fix-promote.log 2>&1
success=0
for attempt in $(seq 1 30); do
 ip=$(docker inspect "$service" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
 if curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null 2>&1; then success=1; break; fi
 sleep 2
done
[[ "$success" == 1 ]]
[[ $(docker inspect "$service" --format '{{.Image}}') == "$(<api-fixed-image.txt)" ]]
while read -r name id; do
 [[ "$name" == "$service" ]] && continue
 [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
done < api-fix-containers.before.txt
date -u +%FT%TZ > api-fixed.txt
trap - EXIT
echo 'Scope-suffix matching fix deployed; no migration or other service restart.'

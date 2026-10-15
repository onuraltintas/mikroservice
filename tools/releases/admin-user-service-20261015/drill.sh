#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/admin-user-service-20261015
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 8
test -s prepared.txt; test -s built.txt; test -s preview-verified.txt; test ! -e deployed.txt
rm -f drill-verified.txt drill-images.sha256
sha256sum -c payload.sha256 > payload-verified.log
database=admin_user_service_drill_20261015
image=eduivme/identity-service:admin-user-service-20261015
verifyCandidates() {
  local service
  for service in coaching-service notification-service identity-service admin-panel; do
    [[ $(docker image inspect "eduivme/$service:admin-user-service-20261015" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]
  done
}
verifyCandidates
api=admin-user-drill-api-20261015
rabbit=admin-user-drill-rabbit-20261015
redis=admin-user-drill-redis-20261015
[[ $(docker inspect eduivme-production-identity-service-1 --format '{{.Image}}') == "$(<previous-identity-service-image.txt)" ]]
db_user=$(docker exec postgres printenv POSTGRES_USER)
if test ! -s identity.before-enrollment.backup; then
  docker exec postgres pg_dump -U "$db_user" -d identity_db -Fc > identity.before-enrollment.backup
  sha256sum identity.before-enrollment.backup > backup.sha256
fi
sha256sum -c backup.sha256 > backup-verified.log
if [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]; then
  docker exec postgres createdb -U "$db_user" "$database"
  docker exec -i postgres pg_restore -U "$db_user" -d "$database" --exit-on-error --no-owner --no-privileges < identity.before-enrollment.backup > restore.log 2>&1
  date -u +%FT%TZ > database-created.txt
else
  test -s database-created.txt
fi
for container in "$api" "$rabbit" "$redis"; do
  if docker inspect "$container" >/dev/null 2>&1; then echo 'Drill container already exists' >&2; exit 2; fi
done
containers=()
cleanup() {
  for container in "${containers[@]}"; do
    [[ $(docker inspect "$container" --format '{{index .Config.Labels "eduivme.drill"}}') == admin-user-service-20261015 ]] || continue
    docker inspect "$container" --format '{{json .State}}' > "$container-state.json"
    docker logs "$container" > "$container-output.log" 2>&1 || true
    docker rm -fv "$container" >/dev/null 2>&1 || true
  done
}
trap cleanup EXIT
rabbit_image=$(docker inspect rabbitmq --format '{{.Image}}')
rabbit_uid=$(docker exec rabbitmq id -u rabbitmq)
rabbit_gid=$(docker exec rabbitmq id -g rabbitmq)
drill_user=admin-user-drill
drill_password=$(openssl rand -hex 20)
export REGISTRATION_DRILL_PASSWORD=$drill_password
printf 'RABBITMQ_DEFAULT_USER=%s\nRABBITMQ_DEFAULT_PASS=%s\n' "$drill_user" "$drill_password" > rabbit-drill.env
printf 'RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS=+S 2:2 +SDcpu 1 +SDio 1\nRABBITMQ_CTL_ERL_ARGS=+S 1:1 +SDcpu 1 +SDio 1\n' >> rabbit-drill.env
docker run -d --name "$rabbit" --user "$rabbit_uid:$rabbit_gid" --label eduivme.drill=admin-user-service-20261015 --network eduplatform-production \
  --tmpfs "/var/lib/rabbitmq:rw,uid=$rabbit_uid,gid=$rabbit_gid,mode=0700" --env-file rabbit-drill.env "$rabbit_image" > rabbit-drill.log
containers+=("$rabbit")
redis_image=$(docker inspect redis --format '{{.Image}}')
docker run -d --name "$redis" --label eduivme.drill=admin-user-service-20261015 --network eduplatform-production "$redis_image" redis-server --save '' --appendonly no > redis-drill.log
containers+=("$redis")
for attempt in $(seq 1 20); do
  if docker exec "$redis" redis-cli ping 2>/dev/null | grep -qx PONG; then break; fi
  sleep 1
done
docker exec "$redis" redis-cli ping | grep -qx PONG
for attempt in $(seq 1 40); do
  [[ $(docker inspect "$rabbit" --format '{{.State.Running}}') == true ]] || exit 1
  if docker exec "$rabbit" rabbitmq-diagnostics -q ping >/dev/null 2>&1; then break; fi
  sleep 2
done
docker exec "$rabbit" rabbitmq-diagnostics -q ping >/dev/null
connection=$(jq -er '.ConnectionStrings__DefaultConnection' identity-service.environment.json)
[[ "$connection" == *'Database=identity_db;'* ]]
export REGISTRATION_CONNECTION=${connection/Database=identity_db;/Database=$database;}
jq -r --arg host "$rabbit" --arg redis "$redis" --arg user "$drill_user" \
  '.ConnectionStrings__DefaultConnection=env.REGISTRATION_CONNECTION | .POSTGRES_DB_IDENTITY="admin_user_service_drill_20261015"
   | .RabbitMQ__Host=$host | .RABBITMQ_HOST=$host
   | .RabbitMQ__Username=$user | .RABBITMQ_DEFAULT_USER=$user
   | .RabbitMQ__Password=env.REGISTRATION_DRILL_PASSWORD | .RABBITMQ_DEFAULT_PASS=env.REGISTRATION_DRILL_PASSWORD
   | .ConnectionStrings__Redis=($redis+":6379,abortConnect=false") | .REDIS_HOST=$redis | .REDIS_PORT="6379" | .REDIS_PASSWORD=""
   | .ASPNETCORE_ENVIRONMENT="Development" | .DOTNET_ENVIRONMENT="Development" | .ENVIRONMENT="Development"
   | .AuthRecaptcha__Enabled="false" | .DataProtection__KeysPath="/tmp/admin-user-drill-keys"
   | .DataProtection__CertificatePath="" | .DataProtection__CertificatePassword=""
   | to_entries[] | "\(.key)=\(.value)"' identity-service.environment.json > api-drill.env
grep -qx 'ASPNETCORE_ENVIRONMENT=Development' api-drill.env
grep -qx 'POSTGRES_DB_IDENTITY=admin_user_service_drill_20261015' api-drill.env
grep -qx 'AuthRecaptcha__Enabled=false' api-drill.env
docker run -d --name "$api" --label eduivme.drill=admin-user-service-20261015 --network eduplatform-production \
  --env-file api-drill.env "$image" > api-drill.log
containers+=("$api")
for attempt in $(seq 1 40); do
  [[ $(docker inspect "$api" --format '{{.State.Running}}') == true ]] || exit 1
  ip=$(docker inspect "$api" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 "http://$ip:8080/health/ready" >/dev/null; then break; fi
  sleep 2
done
curl -fsS --max-time 5 "http://$ip:8080/health/ready" >/dev/null
# Guard the actual running preview, not just the intended environment file.
docker inspect "$api" | jq -e '.[0].HostConfig.PortBindings == {} or .[0].HostConfig.PortBindings == null' >/dev/null
docker inspect "$api" | jq -e '.[0].Config.Env | any(. == "POSTGRES_DB_IDENTITY=admin_user_service_drill_20261015")' >/dev/null
docker inspect "$api" | jq -e --arg host "$rabbit" '.[0].Config.Env | any(. == ("RABBITMQ_HOST="+$host))' >/dev/null
docker inspect "$api" | jq -e --arg host "$redis" '.[0].Config.Env | any(. == ("REDIS_HOST="+$host)) and any(. == ("ConnectionStrings__Redis="+$host+":6379,abortConnect=false"))' >/dev/null
export REGISTRATION_API_BASE="http://$ip:8080"
docker run --rm --network eduplatform-production --entrypoint dotnet --env-file api-drill.env --env REGISTRATION_CONNECTION --env REGISTRATION_API_BASE \
  -v "$release/payload/verification:/verification:ro" "$image" /verification/Verification.dll > relational-results.json
jq -e '.passed >= 15 and (.checks | index("mfa-required")) != null and (.checks | index("legacy-bulk-constructor-compatible")) != null' relational-results.json >/dev/null
cleanup; containers=()
docker exec postgres dropdb -U "$db_user" "$database"
verifyCandidates
sha256sum built-*-image.txt > drill-images.sha256
date -u +%FT%TZ > drill-verified.txt
cat relational-results.json

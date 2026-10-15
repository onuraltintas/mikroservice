#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/speed-reading-assignment-fixes-20261015
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=speed-reading-assignment-fixes-20261015
services=(speed-reading-service speed-reading-frontend staff-portal)
revision=$(tr -d '\r\n' < source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
health() {
  local target=$1 service=$2 ip port=8080 path=/health/ready host=masterhizliokuma.com html script
  [[ "$service" != speed-reading-frontend ]] || { port=80; path=/auth/login; }
  [[ "$service" != staff-portal ]] || { port=80; path=/; host=onuraltintas.net; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && html=$(curl -fsS --max-time 5 -H "Host: $host" "http://$ip:$port$path"); then
      if [[ "$service" == staff-portal ]]; then
        grep -qF '<base href="/staff/">' <<< "$html" || return 1
        script=$(grep -oE 'src="main-[^"]+\.js"' <<< "$html" | cut -d'"' -f2)
        [[ "$script" =~ ^main-[A-Za-z0-9_-]+\.js$ ]] || return 1
        curl -fsS --max-time 5 "http://$ip:80/$script" >/dev/null || return 1
      fi
      return 0
    fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    case "$name" in eduivme-production-speed-reading-service-1|eduivme-production-speed-reading-frontend-1|eduivme-production-staff-portal-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
case "${1:-validate}" in
 prepare)
  test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
  [[ $(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Image}}') == sha256:cb8392f347f054c0b2feb985f8f3ce3a852979d928bc95c0ec0859fd577cb74b ]]
  [[ $(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.Image}}') == sha256:db91e15a852b1bc1f9da27f4dde71a2b4d0f4f6ed1f7ef25e6645756346ce64c ]]
  [[ $(docker inspect eduivme-production-staff-portal-1 --format '{{.Image}}') == sha256:51981086032013624dc0f552b5360ad4cf4a36e33840ab82f48abc48bd186c26 ]]
  [[ $(find payload/speed-api -type f | wc -l) == 4 ]]
  for dll in SpeedReading.Domain.dll SpeedReading.Infrastructure.dll; do
    [[ $(sha256sum "payload/speed-api/$dll" | cut -d' ' -f1) == $(sha256sum "payload/verification/$dll" | cut -d' ' -f1) ]]
  done
  docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
  docker inspect eduivme-production-speed-reading-service-1 | jq -r '.[0].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' > compose-files.txt
  files=("${compose[@]}"); while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
  export TEEN_API_IMAGE; TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
  "${files[@]}" config --format json > base.compose.json
  docker inspect $(cut -d' ' -f1 previous-containers.txt) | jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > images.json
  jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
  for service in "${services[@]}"; do
    docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
    docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
    docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > "$service.environment.json"
    jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json; mv next.json frozen.compose.json
  done
  jq 'walk(if type == "string" then gsub("\\$"; "$$") else . end)' frozen.compose.json > rollback.compose.json
  jq --arg tag "$tag" '.services["speed-reading-service"].image=("eduivme/speed-reading-service:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag) | .services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  date -u +%FT%TZ > prepared.txt
  ;;
 build)
  test -s prepared.txt; test ! -e built.txt
  sha256sum -c payload.sha256 > payload-verified.log
  for service in "${services[@]}"; do
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
    docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
    case "$service" in speed-reading-service) dockerfile=api.Dockerfile;; speed-reading-frontend) dockerfile=frontend.Dockerfile;; staff-portal) dockerfile=staff.Dockerfile;; esac
    docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
    docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
  done
  for service in speed-reading-frontend staff-portal; do
    preview="$tag-$service-preview"
    ! docker inspect "$preview" >/dev/null 2>&1
    trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
    "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" "$service" > "$service-preview.log"
    health "$preview" "$service"
    docker rm -f "$preview" >/dev/null; trap - EXIT
  done
  unchanged; date -u +%FT%TZ > built.txt
  ;;
 drill)
  test -s built.txt; test ! -e drill-verified.txt; test ! -e deployed.txt
  database=speed_maintenance_drill_20261015
  connection=$(jq -er '.ConnectionStrings__SpeedReadingOwned' speed-reading-service.environment.json)
  [[ "$connection" == *'Database=speedreading_owned_db;'* ]]
  username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; service_user=${BASH_REMATCH[2]}
  [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
  docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-drill.backup
  docker exec postgres createdb -U "$db_user" -O "$service_user" "$database"
  cleanupDrill() { local status=$?; trap - EXIT; docker exec postgres dropdb -U "$db_user" "$database" > drill-cleanup.log 2>&1 || { echo 'Named clone cleanup failed' >&2; exit 70; }; exit "$status"; }; trap cleanupDrill EXIT
  docker exec -i postgres pg_restore -U "$db_user" --role "$service_user" -d "$database" --exit-on-error --no-owner --no-privileges < speed.before-drill.backup > restore.log 2>&1
  export SPEED_MAINTENANCE_DRILL_CONNECTION=${connection/Database=speedreading_owned_db;/Database=$database;}
  docker run --rm --network eduplatform-production --entrypoint sh --env SPEED_MAINTENANCE_DRILL_CONNECTION -v "$release/payload/verification:/verification:ro" "eduivme/speed-reading-service:$tag" -c 'cp -a /verification /tmp/verification && cp /app/*.dll /tmp/verification/ && dotnet /tmp/verification/Verification.dll' > relational-results.log 2> relational-error.log
  tail -1 relational-results.log > relational-results.json
  jq -e '.passed >= 65 and (.checks | index("tracking-completed-without-score")) != null and (.checks | index("migration-completed")) != null' relational-results.json >/dev/null
  docker exec postgres dropdb -U "$db_user" "$database"; trap - EXIT
  unchanged; sha256sum payload/speed-api/*.dll > drilled-assemblies.sha256
  date -u +%FT%TZ > drill-verified.txt; cat relational-results.json
  ;;
 promote)
  test -s built.txt; test -s drill-verified.txt; test ! -e deployed.txt
  sha256sum -c payload.sha256 > payload-verified.log; sha256sum -c drilled-assemblies.sha256 > drilled-assemblies-verified.log
  for service in "${services[@]}"; do
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
    [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]
  done
  unchanged
  rollback() {
    local status=$?; trap - EXIT
    if (( status != 0 )); then
      "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || exit 70
      for service in "${services[@]}"; do
        health "eduivme-production-$service-1" "$service" || exit 70
        [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || exit 70
      done
      echo 'Previous application images restored; measurement NULL repair remains valid' >&2
    fi
    exit "$status"
  }; trap rollback EXIT
  # Quiesce the old writer so it cannot insert another synthetic zero during repair.
  "${compose[@]}" -f rollback.compose.json stop speed-reading-service > quiesce.log 2>&1
  docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-promotion.backup
  "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never --entrypoint dotnet speed-reading-service SpeedReading.API.dll --migrate-only > migration.log 2>&1
  [[ $(docker exec postgres psql -U "$db_user" -d speedreading_owned_db -Atc 'SELECT count(*) FROM speed_reading.__ef_migrations_history WHERE "MigrationId" = '\''20261015163000_ClearUnmeasuredAssignmentMetrics'\'';') == 1 ]]
  for service in "${services[@]}"; do
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
    health "eduivme-production-$service-1" "$service"
  done
  curl -fsS --max-time 15 https://masterhizliokuma.com/auth/login >/dev/null
  curl -fsS --max-time 15 https://onuraltintas.net/staff/ | grep -qF '<base href="/staff/">'
  unchanged; date -u +%FT%TZ > deployed.txt
  ;;
 validate)
  test -s deployed.txt
  for service in "${services[@]}"; do
    health "eduivme-production-$service-1" "$service"
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
    docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}} image={{.Image}}'
  done
  docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
  unchanged; cat deployed.txt
  ;;
 *) exit 2;;
esac

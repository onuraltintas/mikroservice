#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/day-advance-20261015
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=day-advance-20261015
services=(speed-reading-service speed-reading-frontend admin-panel)
revision=$(<source-revision.txt); [[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready host=masterhizliokuma.com
  [[ "$service" != speed-reading-frontend ]] || { port=80; path=/auth/login; }
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; host=eduivme.com; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H "Host: $host" "http://$ip:$port$path" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    case "$name" in eduivme-production-speed-reading-service-1|eduivme-production-speed-reading-frontend-1|eduivme-production-admin-panel-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
runVerifier() {
  docker run --rm --network eduplatform-production --entrypoint sh --env DAY_ADVANCE_CONNECTION \
    -v "$release/payload/verification:/verification:ro" "eduivme/speed-reading-service:$tag" \
    -c 'cp -a /verification /tmp/verification && cp /app/*.dll /tmp/verification/ && dotnet /tmp/verification/Verification.dll "$@"' -- "$@"
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload
    [[ $(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Image}}') == sha256:a1750e44674b9aba3753c8e1b89124537ebed2b47f0f8288853985df4900c134 ]]
    [[ $(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.Image}}') == sha256:b9dae675951ad42bcacb668e355483f4687e65a00ed92d156ad7e9e2ddafffce ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:401af83b4ae142e11642b10ec7264356178af750d067aecf8a4f4eebec101d6d ]]
    [[ $(find payload/speed-api -type f | wc -l) == 8 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect eduivme-production-speed-reading-service-1 eduivme-production-admin-panel-1 | jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
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
    jq --arg tag "$tag" '.services["speed-reading-service"].image=("eduivme/speed-reading-service:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag) | .services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      dockerfile=api.Dockerfile; [[ "$service" != speed-reading-frontend ]] || dockerfile=frontend.Dockerfile; [[ "$service" != admin-panel ]] || dockerfile=admin-panel.Dockerfile
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    for dll in payload/speed-api/*.dll; do
      [[ $(sha256sum "$dll" | cut -d' ' -f1) == $(sha256sum "payload/verification/$(basename "$dll")" | cut -d' ' -f1) ]]
      [[ $(sha256sum "$dll" | cut -d' ' -f1) == $(docker run --rm --network none --entrypoint sha256sum "eduivme/speed-reading-service:$tag" "/app/$(basename "$dll")" | cut -d' ' -f1) ]]
    done
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  drill)
    test -s built.txt; test ! -e deployed.txt; test ! -e drill-verified.txt; verifyPayload
    database=day_advance_drill_20261015
    connection=$(jq -er '.ConnectionStrings__SpeedReadingOwned' speed-reading-service.environment.json)
    [[ "$connection" == *'Database=speedreading_owned_db;'* ]]
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; service_user=${BASH_REMATCH[2]}
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-drill.backup
    docker exec postgres createdb -U "$db_user" -O "$service_user" "$database"
    cleanupDrill() { local status=$?; trap - EXIT; docker exec postgres dropdb -U "$db_user" "$database" > drill-cleanup.log 2>&1 || exit 70; exit "$status"; }; trap cleanupDrill EXIT
    docker exec -i postgres pg_restore -U "$db_user" --role "$service_user" -d "$database" --exit-on-error --no-owner --no-privileges < speed.before-drill.backup > restore.log 2>&1
    export DAY_ADVANCE_CONNECTION=${connection/Database=speedreading_owned_db;/Database=$database;}
    runVerifier > drill-result.json 2> drill-error.log
    jq -e '.passed and (.production == false) and .preservedTables > 30' drill-result.json >/dev/null
    docker exec postgres dropdb -U "$db_user" "$database"; trap - EXIT
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat drill-result.json
    ;;
  promote)
    test -s built.txt; test -s drill-verified.txt; test ! -e deployed.txt; verifyPayload
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
          waitReady "eduivme-production-$service-1" "$service" || exit 70
          [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || exit 70
        done
        echo 'Prior application images restored; additive schema retained' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    # Quiesce the sole owned-database writer while comparing every existing row.
    docker stop --time 30 eduivme-production-speed-reading-service-1 > migration-writer-stop.log
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-production-migration.backup
    export DAY_ADVANCE_CONNECTION; DAY_ADVANCE_CONNECTION=$(jq -er '.ConnectionStrings__SpeedReadingOwned' speed-reading-service.environment.json)
    runVerifier --migrate-production > migration-result.json 2> migration-error.log
    jq -e '.passed and .production and .preservedTables > 30' migration-result.json >/dev/null
    for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1; waitReady "eduivme-production-$service-1" "$service"; done
    curl -fsS --max-time 15 https://masterhizliokuma.com/auth/login >/dev/null
    curl -fsS --max-time 15 https://eduivme.com/auth/login >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat migration-result.json; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

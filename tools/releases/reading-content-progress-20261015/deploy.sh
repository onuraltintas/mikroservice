#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/reading-content-progress-20261015
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=reading-content-progress-20261015
services=(speed-reading-service speed-reading-frontend)
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
waitReady() {
  local target=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != speed-reading-frontend ]] || { port=80; path=/auth/login; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H 'Host: masterhizliokuma.com' "http://$ip:$port$path" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    [[ "$name" == eduivme-production-speed-reading-service-1 || "$name" == eduivme-production-speed-reading-frontend-1 ]] && continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Image}}') == sha256:f41b9cf5f0193d8299473c9c8aa101e28fa4a748a0c73da3725c1fbc03f04c54 ]]
    [[ $(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.Image}}') == sha256:4dad6b775af9289e9812136174f760d6abcf282dbf85c232c6bcc8ff4a438b8d ]]
    test -f payload/speed-api/SpeedReading.Infrastructure.dll; test -f payload/speed-api/SpeedReading.Infrastructure.pdb
    [[ $(find payload/speed-api -type f | wc -l) == 2 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect eduivme-production-speed-reading-service-1 eduivme-production-speed-reading-frontend-1 | jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
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
    jq --arg tag "$tag" '.services["speed-reading-service"].image=("eduivme/speed-reading-service:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    rm -f built.txt drill-verified.txt drilled-image.txt drilled-assemblies.sha256
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      dockerfile=api.Dockerfile; [[ "$service" != speed-reading-frontend ]] || dockerfile=frontend.Dockerfile
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    preview=reading-content-progress-preview-20261015
    docker inspect "$preview" >/dev/null 2>&1 && { echo 'Preview already exists' >&2; exit 2; }
    cleanup() { local status=$?; trap - EXIT; docker rm -f "$preview" >/dev/null 2>&1 || true; exit "$status"; }; trap cleanup EXIT
    docker run -d --network none --name "$preview" "eduivme/speed-reading-frontend:$tag" > preview-container.txt
    for attempt in $(seq 1 40); do if docker exec "$preview" wget -q -O /dev/null http://127.0.0.1:80/auth/login; then break; fi; sleep 2; done
    for path in /student/exercises /auth/login /brand/master-favicon.svg; do docker exec "$preview" wget -q -O /dev/null "http://127.0.0.1:80$path"; done
    docker rm -f "$preview" >/dev/null; trap - EXIT
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  drill)
    test -s built.txt; test ! -e deployed.txt; test ! -e drill-verified.txt
    database=reading_content_progress_drill_20261015
    connection=$(jq -er '.ConnectionStrings__SpeedReadingOwned' speed-reading-service.environment.json)
    [[ "$connection" == *'Database=speedreading_owned_db;'* ]]
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; service_user=${BASH_REMATCH[2]}
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-drill.backup
    docker exec postgres createdb -U "$db_user" -O "$service_user" "$database"
    cleanupDrill() { local status=$?; trap - EXIT; docker exec postgres dropdb -U "$db_user" "$database" > drill-cleanup.log 2>&1 || { echo 'Named drill database cleanup failed' >&2; exit 70; }; exit "$status"; }; trap cleanupDrill EXIT
    docker exec -i postgres pg_restore -U "$db_user" --role "$service_user" -d "$database" --exit-on-error --no-owner --no-privileges < speed.before-drill.backup > restore.log 2>&1
    export READING_PROGRESS_DRILL_CONNECTION=${connection/Database=speedreading_owned_db;/Database=$database;}
    app_hash=$(sha256sum payload/speed-api/SpeedReading.Infrastructure.dll | cut -d' ' -f1)
    [[ $(sha256sum payload/verification/SpeedReading.Infrastructure.dll | cut -d' ' -f1) == "$app_hash" ]]
    [[ $(docker run --rm --network none --entrypoint sha256sum "eduivme/speed-reading-service:$tag" /app/SpeedReading.Infrastructure.dll | cut -d' ' -f1) == "$app_hash" ]]
    docker run --rm --network eduplatform-production --entrypoint sh --env READING_PROGRESS_DRILL_CONNECTION -v "$release/payload/verification:/verification:ro" "eduivme/speed-reading-service:$tag" -c 'cp -a /verification /tmp/verification && cp /app/*.dll /tmp/verification/ && dotnet /tmp/verification/Verification.dll' > relational-results.json 2> relational-error.log
    jq -e '.passed == 103 and .failed == 0 and .ageMismatchChecks == 6' relational-results.json >/dev/null
    docker exec postgres dropdb -U "$db_user" "$database"; trap - EXIT
    unchanged; cp built-speed-reading-service-image.txt drilled-image.txt
    sha256sum payload/speed-api/*.dll > drilled-assemblies.sha256
    date -u +%FT%TZ > drill-verified.txt; cat relational-results.json
    ;;
  promote)
    test -s built.txt; test -s drill-verified.txt; test ! -e deployed.txt
    [[ "$(<drilled-image.txt)" == "$(<built-speed-reading-service-image.txt)" ]]
    sha256sum -c drilled-assemblies.sha256 > drilled-assemblies-verified.log
    sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]
    done
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || { echo 'CRITICAL: rollback failed' >&2; exit 70; }
        for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service" && [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || { echo 'CRITICAL: rollback verification failed' >&2; exit 70; }; done
        echo 'Previous Speed API and frontend images restored' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1; waitReady "eduivme-production-$service-1" "$service"; done
    curl -fsS --max-time 15 https://masterhizliokuma.com/auth/login >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

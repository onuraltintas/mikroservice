#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/institution-benchmark-20261016
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=institution-benchmark-20261016
container=eduivme-production-speed-reading-service-1
staff=eduivme-production-staff-portal-1
student=eduivme-production-speed-reading-frontend-1
revision=$(<source-revision.txt); [[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
waitReady() {
  local ip
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$container" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 "http://$ip:8080/health/ready" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
waitStaff() {
  local ip html script target=${1:-$staff} base=${2:-/staff/}
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && html=$(curl -fsS --max-time 5 "http://$ip/"); then
      grep -qF "<base href=\"$base\">" <<< "$html" || return 1
      script=$(grep -oE 'src="main-[^"]+\.js"' <<< "$html" | cut -d'"' -f2)
      [[ "$script" =~ ^main-[A-Za-z0-9_-]+\.js$ ]] || return 1
      if [[ $(curl -fsS --max-time 5 -o /dev/null -w '%{http_code}' "http://$ip/$script") == 200 ]]; then
        printf 'static_ready target=%s base=%s html=passed main_js=200\n' "$target" "$base"
        return 0
      fi
    fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    [[ "$name" == "$container" || "$name" == "$staff" || "$name" == "$student" ]] && continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
runVerifier() {
  docker run --rm --network eduplatform-production --entrypoint sh --env REPORTS_CONNECTION \
    -v "$release/payload/verification:/verification:ro" "eduivme/speed-reading-service:$tag" \
    -c 'cp -a /verification /tmp/verification && cp /app/*.dll /tmp/verification/ && dotnet /tmp/verification/Verification.dll "$@"' -- "$@"
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload
    [[ $(docker inspect "$container" --format '{{.Image}}') == sha256:4edf58e412fbb067fdcf21352ef40992d3809d799780be5de92a7ada421631b9 ]]
    [[ $(docker inspect "$staff" --format '{{.Image}}') == sha256:431d619f3021059d1460bfae748e79fa8ce5ee1417a14ee8c818394a4d942b1d ]]
    [[ $(docker inspect "$student" --format '{{.Image}}') == sha256:ff2ad1c23ebaaef0997f08ebe0db9993aa6e006d817ca93581dd2a2a71e17e72 ]]
    [[ $(find payload/speed-api -type f | wc -l) == 6 ]]
    test -s payload/speed-api/SpeedReading.Infrastructure.dll; test -s payload/speed-api/SpeedReading.Infrastructure.pdb
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect "$container" | jq -r '.[0].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' > compose-files.txt
    files=("${compose[@]}"); while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE; TEEN_API_IMAGE=$(docker inspect "$container" --format '{{.Config.Image}}')
    "${files[@]}" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 previous-containers.txt) | jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    docker inspect "$container" --format '{{.Image}}' > previous-image.txt
    docker inspect "$container" --format '{{.Id}}' > previous-container.txt
    docker inspect "$staff" --format '{{.Image}}' > previous-staff-image.txt
    docker inspect "$staff" --format '{{.Id}}' > previous-staff-container.txt
    docker inspect "$student" --format '{{.Image}}' > previous-student-image.txt
    docker inspect "$student" --format '{{.Id}}' > previous-student-container.txt
    docker inspect "$staff" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > staff.environment.json
    jq --slurpfile env staff.environment.json '.services["staff-portal"].environment=$env[0]' frozen.compose.json > next.json; mv next.json frozen.compose.json
    docker inspect "$student" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > student.environment.json
    jq --slurpfile env student.environment.json '.services["speed-reading-frontend"].environment=$env[0]' frozen.compose.json > next.json; mv next.json frozen.compose.json
    docker inspect "$container" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > api.environment.json
    jq --slurpfile env api.environment.json '.services["speed-reading-service"].environment=$env[0] | walk(if type == "string" then gsub("\\$"; "$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["speed-reading-service"].image=("eduivme/speed-reading-service:"+$tag) | .services["staff-portal"].image=("eduivme/staff-portal:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    docker image tag "$(<previous-image.txt)" "eduivme/speed-reading-service:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/speed-reading-service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "eduivme/speed-reading-service:$tag" . > build.log 2>&1
    [[ $(docker image inspect "eduivme/speed-reading-service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    docker image inspect "eduivme/speed-reading-service:$tag" --format '{{.Id}}' > built-image.txt
    [[ $(docker inspect "$staff" --format '{{.Id}}') == "$(<previous-staff-container.txt)" ]]
    docker image tag "$(<previous-staff-image.txt)" "eduivme/staff-portal:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/staff-portal:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f staff.Dockerfile -t "eduivme/staff-portal:$tag" . > staff-build.log 2>&1
    docker image inspect "eduivme/staff-portal:$tag" --format '{{.Id}}' > built-staff-image.txt
    [[ $(docker inspect "$student" --format '{{.Id}}') == "$(<previous-student-container.txt)" ]]
    docker image tag "$(<previous-student-image.txt)" "eduivme/speed-reading-frontend:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/speed-reading-frontend:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f frontend.Dockerfile -t "eduivme/speed-reading-frontend:$tag" . > student-build.log 2>&1
    docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{.Id}}' > built-student-image.txt
    for dll in SpeedReading.API.dll SpeedReading.Application.dll SpeedReading.Infrastructure.dll; do
      [[ $(sha256sum "payload/speed-api/$dll" | cut -d' ' -f1) == $(sha256sum "payload/verification/$dll" | cut -d' ' -f1) ]]
      [[ $(sha256sum "payload/speed-api/$dll" | cut -d' ' -f1) == $(docker run --rm --network none --entrypoint sha256sum "eduivme/speed-reading-service:$tag" "/app/$dll" | cut -d' ' -f1) ]]
    done
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  drill)
    test -s built.txt; test ! -e deployed.txt; test ! -e drill-verified.txt; verifyPayload
    database=institution_benchmark_drill_20261016
    connection=$(jq -er '.ConnectionStrings__SpeedReadingOwned' api.environment.json)
    [[ "$connection" == *'Database=speedreading_owned_db;'* ]]
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; service_user=${BASH_REMATCH[2]}
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-drill.backup
    docker exec postgres createdb -U "$db_user" -O "$service_user" "$database"
    cleanupDrill() { local status=$?; trap - EXIT; docker exec postgres dropdb -U "$db_user" "$database" > drill-cleanup.log 2>&1 || exit 70; exit "$status"; }; trap cleanupDrill EXIT
    docker exec -i postgres pg_restore -U "$db_user" --role "$service_user" -d "$database" --exit-on-error --no-owner --no-privileges < speed.before-drill.backup > restore.log 2>&1
    export REPORTS_CONNECTION=${connection/Database=speedreading_owned_db;/Database=$database;}
    runVerifier > drill-result.json 2> drill-error.log
    jq -e '.passed and (.readOnly == false) and .scenarios == 10 and .preservedTables > 30' drill-result.json >/dev/null
    docker exec postgres dropdb -U "$db_user" "$database"; trap - EXIT
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat drill-result.json
    ;;
  preview)
    test -s built.txt; test ! -e deployed.txt; verifyPayload
    preview="$tag-staff-preview"
    ! docker inspect "$preview" >/dev/null 2>&1
    trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
    "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" staff-portal > staff-preview.log
    waitStaff "$preview"
    docker rm -f "$preview" >/dev/null; trap - EXIT
    preview="$tag-student-preview"
    ! docker inspect "$preview" >/dev/null 2>&1
    trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
    "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" speed-reading-frontend > student-preview.log
    waitStaff "$preview" /
    docker rm -f "$preview" >/dev/null; trap - EXIT
    unchanged; date -u +%FT%TZ > preview-verified.txt
    ;;
  promote)
    test -s built.txt; test -s drill-verified.txt; test ! -e deployed.txt; verifyPayload
    test -s preview-verified.txt
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    [[ $(docker inspect "$staff" --format '{{.Id}}') == "$(<previous-staff-container.txt)" ]]
    [[ $(docker inspect "$student" --format '{{.Id}}') == "$(<previous-student-container.txt)" ]]
    [[ $(docker image inspect "eduivme/speed-reading-service:$tag" --format '{{.Id}}') == "$(<built-image.txt)" ]]
    [[ $(docker image inspect "eduivme/staff-portal:$tag" --format '{{.Id}}') == "$(<built-staff-image.txt)" ]]
    [[ $(docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{.Id}}') == "$(<built-student-image.txt)" ]]
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never speed-reading-service staff-portal speed-reading-frontend > rollback.log 2>&1 || exit 70
        waitReady || exit 70
        [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<previous-image.txt)" ]] || exit 70
        [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<previous-staff-image.txt)" ]] || exit 70
        waitStaff || exit 70
        [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<previous-student-image.txt)" ]] || exit 70
        waitStaff "$student" / || exit 70
        echo 'Previous API, staff and student images restored; no schema or data migration performed' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    docker stop --time 30 "$container" > writer-stop.log
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-promotion.backup
    export REPORTS_CONNECTION; REPORTS_CONNECTION=$(jq -er '.ConnectionStrings__SpeedReadingOwned' api.environment.json)
    runVerifier --production-read-only > before.json 2> before-error.log
    # Compare while the application writer is stopped; legitimate student writes must not trigger rollback.
    "${compose[@]}" -f production.compose.json up --no-start --no-deps --pull never speed-reading-service > promotion.log 2>&1
    runVerifier --production-read-only > after.json 2> after-error.log
    for phase in before after; do
      jq -e '.passed and .readOnly and .pendingMigrations == 0' "$phase.json" >/dev/null
      jq -S '.fingerprints' "$phase.json" > "$phase-history.json"
    done
    cmp before-history.json after-history.json
    "${compose[@]}" -f production.compose.json up -d --no-deps --no-recreate --pull never speed-reading-service >> promotion.log 2>&1
    waitReady
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never staff-portal >> promotion.log 2>&1
    [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<built-staff-image.txt)" ]]
    waitStaff
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never speed-reading-frontend >> promotion.log 2>&1
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-student-image.txt)" ]]
    waitStaff "$student" /
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt; waitReady; waitStaff; waitStaff "$student" /
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<built-staff-image.txt)" ]]
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-student-image.txt)" ]]
    unchanged
    docker inspect "$container" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
    jq 'del(.fingerprints)' before.json after.json; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

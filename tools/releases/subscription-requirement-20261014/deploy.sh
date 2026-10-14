#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/subscription-requirement-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=subscription-requirement-20261014
services=(speed-reading-service admin-panel)
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
waitService() {
  local container=$1 service=$2 ip port=8080 path=/health/ready
  local -a headers=()
  if [[ "$service" == admin-panel ]]; then port=4000; path=/auth/login; headers=(-H 'Host: eduivme.com'); fi
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$container" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 "${headers[@]}" "http://$ip:$port$path" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local container id
  while read -r container id; do
    [[ "$container" == eduivme-production-speed-reading-service-1 || "$container" == eduivme-production-admin-panel-1 ]] && continue
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $container" >&2; return 1; }
  done < previous-containers.txt
}
bankFingerprint() {
  docker exec postgres psql -U "$db_user" -d speedreading_owned_db -Atc 'SELECT coalesce(jsonb_agg(to_jsonb(s) - '"'"'RequireActiveSubscription'"'"' ORDER BY "Id"), '"'"'[]'"'"'::jsonb) FROM speed_reading.bank_transfer_payment_settings s' | sha256sum | cut -d' ' -f1
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Image}}') == sha256:2cb11e82836c120ad48a3d25bf4b09339b1e5cd94d1edb4d95356caf24e89eab ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:5cf5e6482aa40c81ecd699022f230b4091a0087cf519e997aca1afd3a5564903 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r container; do docker inspect "$container" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect eduivme-production-speed-reading-service-1 eduivme-production-admin-panel-1 | jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
    files=("${compose[@]}")
    while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE
    TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
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
    jq --arg tag "$tag" '.services["speed-reading-service"].image=("eduivme/speed-reading-service:"+$tag) | .services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    rm -f built.txt preview-verified.txt
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      dockerfile=speed-api.Dockerfile; [[ "$service" != admin-panel ]] || dockerfile=admin.Dockerfile
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    done
    preview=subscription-admin-preview-20261014
    docker inspect "$preview" >/dev/null 2>&1 && { echo 'Preview already exists' >&2; exit 2; }
    cleanupPreview() { local status=$?; trap - EXIT; docker rm -f "$preview" >/dev/null 2>&1 || true; exit "$status"; }
    trap cleanupPreview EXIT
    docker run -d --network none --name "$preview" "eduivme/admin-panel:$tag" > admin-preview.txt
    for attempt in $(seq 1 40); do
      if docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))' >/dev/null 2>&1; then break; fi
      sleep 2
    done
    docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))'
    docker rm -f "$preview" >/dev/null
    trap - EXIT
    for service in "${services[@]}"; do docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"; done
    date -u +%FT%TZ > preview-verified.txt
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  drill)
    test -s built.txt; test ! -e deployed.txt; test ! -e drill-verified.txt
    database=subscription_requirement_drill_20261014
    connection=$(jq -er '.ConnectionStrings__SpeedReadingOwned' speed-reading-service.environment.json)
    [[ "$connection" == *'Database=speedreading_owned_db;'* ]]
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'
    [[ "$connection" =~ $username_regex ]]
    service_user=${BASH_REMATCH[2]}
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-drill.backup
    sha256sum speed.before-drill.backup > backup.sha256
    docker exec postgres createdb -U "$db_user" -O "$service_user" "$database"
    cleanupDrill() {
      local status=$?; trap - EXIT
      if [[ "$database" == subscription_requirement_drill_20261014 && -s drill-created.txt ]]; then
        docker exec postgres dropdb -U "$db_user" "$database" > drill-cleanup.log 2>&1 || { echo 'Named drill database cleanup failed' >&2; (( status != 0 )) || status=70; }
      fi
      exit "$status"
    }
    date -u +%FT%TZ > drill-created.txt
    trap cleanupDrill EXIT
    docker exec -i postgres pg_restore -U "$db_user" --role "$service_user" -d "$database" --exit-on-error --no-owner --no-privileges < speed.before-drill.backup > restore.log 2>&1
    export SUBSCRIPTION_DRILL_CONNECTION=${connection/Database=speedreading_owned_db;/Database=$database;}
    docker run --rm --network eduplatform-production --entrypoint dotnet --env SUBSCRIPTION_DRILL_CONNECTION -v "$release/payload/verification:/verification:ro" "eduivme/speed-reading-service:$tag" /verification/Verification.dll > relational-results.json 2>relational-error.log
    jq -e '.passed >= 29 and (.checks | index("concurrent-same-key-first-create-with-retry")) != null and (.checks | index("product-boundary-False")) != null' relational-results.json >/dev/null
    docker exec postgres dropdb -U "$db_user" "$database"
    trap - EXIT
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat relational-results.json
    ;;
  promote)
    test -s built.txt; test -s preview-verified.txt; test -s drill-verified.txt; test ! -e deployed.txt
    sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
    for service in "${services[@]}"; do [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]; done
    unchanged
    migration=$(docker exec postgres psql -U "$db_user" -d speedreading_owned_db -Atc 'SELECT "MigrationId" FROM speed_reading.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 1')
    [[ "$migration" == 20261012131123_AddProgramContinuationDecisions || "$migration" == 20261014144635_SubscriptionRequirementToggle ]]
    if [[ "$migration" == 20261014144635_SubscriptionRequirementToggle ]]; then test -s rollback.log; fi
    if test ! -s speed.before-migration.backup; then
      docker exec postgres pg_dump -U "$db_user" -d speedreading_owned_db -Fc > speed.before-migration.backup
      sha256sum speed.before-migration.backup > production-backup.sha256
    fi
    sha256sum -c production-backup.sha256 > production-backup-verified.log
    bankFingerprint > bank.before.sha256
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || { echo 'CRITICAL: rollback failed' >&2; exit 70; }
        for service in "${services[@]}"; do waitService "eduivme-production-$service-1" "$service" && [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || { echo 'CRITICAL: rollback verification failed' >&2; exit 70; }; done
        echo 'Previous images restored; additive column retained' >&2
      fi
      exit "$status"
    }
    trap rollback EXIT
    if [[ "$migration" == 20261012131123_AddProgramContinuationDecisions ]]; then
      docker exec -i -e 'PGOPTIONS=-c lock_timeout=10s -c statement_timeout=60s' postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d speedreading_owned_db < migration.sql > migration.log 2>&1
    fi
    [[ $(bankFingerprint) == "$(<bank.before.sha256)" ]]
    [[ $(docker exec postgres psql -U "$db_user" -d speedreading_owned_db -Atc 'SELECT count(*) FROM speed_reading.bank_transfer_payment_settings WHERE NOT "RequireActiveSubscription"') == 0 ]]
    for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1; waitService "eduivme-production-$service-1" "$service"; done
    for url in https://eduivme.com/auth/login https://masterhizliokuma.com/auth/login https://onuraltintas.net/staff/; do curl -fsS --max-time 15 "$url" >/dev/null; done
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
    for service in "${services[@]}"; do waitService "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}')" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat deployed.txt
    ;;
  retry-after-rollback)
    test -s rollback.log; test -s drill-verified.txt; test ! -e deployed.txt
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]]
      waitService "eduivme-production-$service-1" "$service"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
    done
    unchanged; date -u +%FT%TZ > rollback-verified.txt
    ;;
  *) echo 'Supported: prepare/build/drill/promote/validate' >&2; exit 2 ;;
esac

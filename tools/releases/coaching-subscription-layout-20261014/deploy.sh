#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-subscription-layout-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-subscription-layout-20261014
container=eduivme-production-admin-panel-1
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
waitReady() {
  local target=$1 ip
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H 'Host: eduivme.com' "http://$ip:4000/auth/login" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    [[ "$name" == "$container" ]] && continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
settingsFingerprint() {
  local db_user
  db_user=$(docker exec postgres printenv POSTGRES_USER)
  docker exec postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db -Atc 'SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY "Id"), '"'"'[]'"'"'::jsonb) FROM coaching.subscription_settings s' | sha256sum | cut -d' ' -f1
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect "$container" --format '{{.Image}}') == sha256:31490b506b71a00a1feab8cf854072f111c71fe3aa5b990ca156e33358ca168e ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect "$container" --format '{{.Image}}' > previous-image.txt
    docker inspect "$container" --format '{{.Id}}' > previous-container.txt
    docker inspect "$container" | jq -r '.[0].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' > compose-files.txt
    files=("${compose[@]}")
    while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE
    TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
    "${files[@]}" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 previous-containers.txt) | jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > images.json
    docker inspect "$container" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > admin.environment.json
    jq --slurpfile images images.json --slurpfile env admin.environment.json '. * $images[0] | .services["admin-panel"].environment=$env[0] | walk(if type == "string" then gsub("\\$"; "$$") else . end)' base.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    settingsFingerprint > settings.before.sha256
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    rm -f built.txt preview-verified.txt
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    docker image tag "$(<previous-image.txt)" "eduivme/admin-panel:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/admin-panel:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f admin.Dockerfile -t "eduivme/admin-panel:$tag" . > build.log 2>&1
    [[ $(docker image inspect "eduivme/admin-panel:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    preview=coaching-subscription-layout-preview-20261014
    docker inspect "$preview" >/dev/null 2>&1 && { echo 'Preview already exists' >&2; exit 2; }
    cleanup() { local status=$?; trap - EXIT; docker rm -f "$preview" >/dev/null 2>&1 || true; exit "$status"; }
    trap cleanup EXIT
    docker run -d --network none --name "$preview" "eduivme/admin-panel:$tag" > preview-container.txt
    for attempt in $(seq 1 40); do
      if docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))' >/dev/null 2>&1; then break; fi
      sleep 2
    done
    docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))'
    docker rm -f "$preview" >/dev/null; trap - EXIT
    docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}' > built-image.txt
    unchanged; date -u +%FT%TZ > preview-verified.txt; date -u +%FT%TZ > built.txt
    ;;
  promote)
    test -s built.txt; test -s preview-verified.txt; test ! -e deployed.txt
    sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    [[ $(docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}') == "$(<built-image.txt)" ]]
    [[ $(settingsFingerprint) == "$(<settings.before.sha256)" ]]
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never admin-panel > rollback.log 2>&1 || { echo 'CRITICAL: rollback failed' >&2; exit 70; }
        waitReady "$container" && [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<previous-image.txt)" ]] || { echo 'CRITICAL: rollback verification failed' >&2; exit 70; }
        echo 'Previous admin image restored' >&2
      fi
      exit "$status"
    }
    trap rollback EXIT
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never admin-panel > promotion.log 2>&1
    waitReady "$container"
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    curl -fsS --max-time 15 https://eduivme.com/auth/login >/dev/null
    [[ $(settingsFingerprint) == "$(<settings.before.sha256)" ]]
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt; waitReady "$container"
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    [[ $(settingsFingerprint) == "$(<settings.before.sha256)" ]]
    unchanged; docker inspect "$container" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

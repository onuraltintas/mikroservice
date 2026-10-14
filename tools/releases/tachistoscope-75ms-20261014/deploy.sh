#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/tachistoscope-75ms-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=tachistoscope-75ms-20261014
container=eduivme-production-speed-reading-frontend-1
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
waitReady() {
  local target=$1 ip
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H 'Host: masterhizliokuma.com' "http://$ip:80/auth/login" >/dev/null; then return 0; fi
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

case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect "$container" --format '{{.Image}}') == sha256:ccddec671d2948cf0be96473edb872026e683167ae8336f17a63006d0f4f4bf0 ]]
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
    docker inspect "$container" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > frontend.environment.json
    jq --slurpfile images images.json --slurpfile env frontend.environment.json '. * $images[0] | .services["speed-reading-frontend"].environment=$env[0] | walk(if type == "string" then gsub("\\$"; "$$") else . end)' base.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    rm -f built.txt preview-verified.txt
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    docker image tag "$(<previous-image.txt)" "eduivme/speed-reading-frontend:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/speed-reading-frontend:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f frontend.Dockerfile -t "eduivme/speed-reading-frontend:$tag" . > build.log 2>&1
    [[ $(docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    preview=tachistoscope-75ms-preview-20261014
    docker inspect "$preview" >/dev/null 2>&1 && { echo 'Preview already exists' >&2; exit 2; }
    cleanup() { local status=$?; trap - EXIT; docker rm -f "$preview" >/dev/null 2>&1 || true; exit "$status"; }
    trap cleanup EXIT
    docker run -d --network none --name "$preview" "eduivme/speed-reading-frontend:$tag" > preview-container.txt
    for attempt in $(seq 1 40); do
      if docker exec "$preview" wget -q -O /dev/null http://127.0.0.1:80/auth/login; then break; fi
      sleep 2
    done
    for path in /student/exercises /auth/login /auth/register /auth/register-teacher /auth/register-school /brand/master-favicon.svg; do docker exec "$preview" wget -q -O /dev/null "http://127.0.0.1:80$path"; done
    docker exec "$preview" sh -c 'grep -qF "brand/master-favicon.svg" /usr/share/nginx/html/index.html && ! grep -qF "href=\"favicon.ico\"" /usr/share/nginx/html/index.html'
    docker rm -f "$preview" >/dev/null; trap - EXIT
    docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{.Id}}' > built-image.txt
    unchanged; date -u +%FT%TZ > preview-verified.txt; date -u +%FT%TZ > built.txt
    ;;
  promote)
    test -s built.txt; test -s preview-verified.txt; test ! -e deployed.txt
    sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    [[ $(docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{.Id}}') == "$(<built-image.txt)" ]]
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never speed-reading-frontend > rollback.log 2>&1 || { echo 'CRITICAL: rollback failed' >&2; exit 70; }
        waitReady "$container" && [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<previous-image.txt)" ]] || { echo 'CRITICAL: rollback verification failed' >&2; exit 70; }
        echo 'Previous Speed frontend image restored' >&2
      fi
      exit "$status"
    }
    trap rollback EXIT
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never speed-reading-frontend > promotion.log 2>&1
    waitReady "$container"
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    curl -fsS --max-time 15 https://masterhizliokuma.com/auth/login >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt; waitReady "$container"
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    unchanged; docker inspect "$container" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

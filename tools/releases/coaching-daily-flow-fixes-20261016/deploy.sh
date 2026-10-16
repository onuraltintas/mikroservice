#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-daily-flow-fixes-20261016-r2
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9 || { echo 'Another coaching deployment is running' >&2; exit 2; }
exec 8>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 8 || { echo 'Another frontend deployment is running' >&2; exit 2; }
name=eduivme-production-admin-panel-1
tag=coaching-daily-flow-fixes-20261016-r2
candidate=coaching-daily-flow-preview-20261016-r2
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
waitWeb() {
  local container=$1 ip
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$container" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H 'Host: eduivme.com' "http://$ip:4000/auth/login" >/dev/null && curl -fsS --max-time 5 -H 'Host: eduivme.com' "http://$ip:4000/coaching-portal" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local container id
  while read -r container id; do
    [[ "$container" == "$name" ]] && continue
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $container" >&2; return 1; }
  done < previous-containers.txt
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt
    sha256sum -c payload.sha256 > payload-verified.log
    docker inspect "$name" --format '{{.Image}}' > previous-image.txt
    [[ $(<previous-image.txt) == sha256:f23cf7b3dc4ac6eb795909f916a59ca13996d6ac4929f74c5a5adda93d5abf20 ]] || { echo 'Frontend baseline changed; rebuild before deployment' >&2; exit 2; }
    docker inspect "$name" --format '{{.Id}}' > previous-container.txt
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}} {{.ID}}' |
      while read -r container _; do docker inspect "$container" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect "$name" | jq -r '.[0].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' > compose-files.txt
    while IFS= read -r file; do test -f "$file"; compose+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE
    TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
    "${compose[@]}" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 previous-containers.txt) | jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > images.json
    docker inspect "$name" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > admin.environment.json
    jq --slurpfile images images.json --slurpfile env admin.environment.json '. * $images[0] | .services["admin-panel"].environment=$env[0] | walk(if type == "string" then gsub("\\$"; "$$") else . end)' base.compose.json > rollback.compose.json
    jq --arg image "eduivme/admin-panel:$tag" '.services["admin-panel"].image=$image' rollback.compose.json > production.compose.json
    "${compose[@]:0:8}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    sha256sum -c payload.sha256 > payload-verified.log
    docker image tag "$(<previous-image.txt)" "eduivme/admin-panel:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/admin-panel:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f admin.Dockerfile -t "eduivme/admin-panel:$tag" . > build.log 2>&1
    [[ $(docker image inspect "eduivme/admin-panel:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    docker container inspect "$candidate" >/dev/null 2>&1 && { echo 'Preview container already exists' >&2; exit 2; }
    "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$candidate" admin-panel > preview.log
    trap 'docker rm -f "$candidate" >/dev/null 2>&1 || true' EXIT
    waitWeb "$candidate"
    unchanged
    date -u +%FT%TZ > preview-verified.txt
    ;;
  promote)
    test -s preview-verified.txt; test ! -e deployed.txt
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    unchanged
    rollback() {
      local status=$?
      trap - EXIT
      if (( status != 0 )); then
        if ! "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never admin-panel > rollback.log 2>&1; then
          echo 'CRITICAL: frontend rollback command failed; inspect rollback.log' >&2; exit 70
        fi
        if ! waitWeb "$name" || [[ $(docker inspect "$name" --format '{{.Image}}') != "$(<previous-image.txt)" ]]; then
          echo 'CRITICAL: previous frontend image/health could not be verified' >&2; exit 70
        fi
        echo 'Frontend restored to previous image' >&2
      fi
      exit "$status"
    }
    trap rollback EXIT
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never admin-panel > promotion.log 2>&1
    waitWeb "$name"
    curl -fsS --max-time 15 https://eduivme.com/auth/login >/dev/null
    curl -fsS --max-time 15 https://eduivme.com/coaching-portal >/dev/null
    unchanged
    [[ $(docker inspect "$name" --format '{{.Image}}') == "$(docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}')" ]]
    date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    waitWeb "$name"; unchanged
    [[ $(docker inspect "$name" --format '{{.Image}}') == "$(docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}')" ]]
    docker inspect "$name" --format 'Frontend status={{.State.Status}} restarts={{.RestartCount}}'
    cat deployed.txt
    ;;
  *) echo 'Supported: prepare/build/promote/validate' >&2; exit 2 ;;
esac

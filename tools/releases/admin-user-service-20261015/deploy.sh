#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/admin-user-service-20261015
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=admin-user-service-20261015
services=(coaching-service notification-service identity-service admin-panel)
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    case "$name" in eduivme-production-coaching-service-1|eduivme-production-notification-service-1|eduivme-production-identity-service-1|eduivme-production-admin-panel-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
verifyBuilt() {
  local service
  for service in "${services[@]}"; do
    [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]
  done
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect eduivme-production-identity-service-1 --format '{{.Image}}') == sha256:13c920fc996712a69cd4ddbb6ddd271a418a89ea1b7e0379e477a25d7f8011f4 ]]
    [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == sha256:6588e953886e32bb39d2fed2960c5bdef2a53c19aee12341fb0df9515a419d6d ]]
    [[ $(docker inspect eduivme-production-notification-service-1 --format '{{.Image}}') == sha256:aa804c3c5b60f71a6dd6af600ed01d268695a96c7bfb3d5b24eed11e5cfcd73b ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:2efe839ffaa4158d71ec4b9585b80f4272ebafb676d95683e2521e374d06aff5 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect eduivme-production-identity-service-1 eduivme-production-admin-panel-1 | jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
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
    jq -e '.AuthRecaptcha__Enabled == "true" and .AuthRecaptcha__MinimumScore == "0.5"' identity-service.environment.json >/dev/null
    jq 'walk(if type == "string" then gsub("\\$"; "$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag) | .services["notification-service"].image=("eduivme/notification-service:"+$tag) | .services["identity-service"].image=("eduivme/identity-service:"+$tag) | .services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; test ! -e deployed.txt
    rm -f built.txt preview-verified.txt drill-verified.txt drill-images.sha256
    sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    preview=admin-user-preview-20261015
    docker inspect "$preview" >/dev/null 2>&1 && { echo 'Preview exists' >&2; exit 2; }
    cleanup() { local status=$?; trap - EXIT; docker rm -f "$preview" >/dev/null 2>&1 || true; exit "$status"; }
    trap cleanup EXIT
    docker run -d --network none --name "$preview" "eduivme/admin-panel:$tag" > preview-container.txt
    for attempt in $(seq 1 40); do
      if docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))' >/dev/null 2>&1; then break; fi
      sleep 2
    done
    docker exec "$preview" node -e 'fetch("http://127.0.0.1:4000/auth/login").then(r=>{if(!r.ok)process.exit(1)}).catch(()=>process.exit(1))'
    docker rm -f "$preview" >/dev/null; trap - EXIT
    unchanged; date -u +%FT%TZ > preview-verified.txt; date -u +%FT%TZ > built.txt
    ;;
  promote)
    test -s built.txt; test -s preview-verified.txt; test -s drill-verified.txt; test ! -e deployed.txt
    sha256sum -c payload.sha256 > payload-verified.log; sha256sum -c drill-images.sha256 >/dev/null; verifyBuilt
    for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        local -a restore=(admin-panel identity-service notification-service coaching-service)
        # New-format events may still be queued. Compatible consumers must remain after producer cutover.
        if test -e producer-cutover-started.txt; then restore=(admin-panel identity-service); fi
        for service in "${restore[@]}"; do
          "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1 || exit 70
          waitReady "eduivme-production-$service-1" "$service" && [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || exit 70
        done
        if test -e producer-cutover-started.txt; then
          for service in coaching-service notification-service; do
            waitReady "eduivme-production-$service-1" "$service" && [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]] || exit 70
          done
          echo 'Admin and Identity restored; compatible consumers retained for queued events' >&2
        else
          echo 'Previous images restored and verified' >&2
        fi
      fi
      exit "$status"
    }
    trap rollback EXIT
    # Consumers must understand explicit product scope before Identity emits it; admin is last.
    for service in "${services[@]}"; do
      [[ "$service" != identity-service ]] || date -u +%FT%TZ > producer-cutover-started.txt
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitReady "eduivme-production-$service-1" "$service"
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
    done
    docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
    curl -fsS --max-time 15 https://eduivme.com/auth/login >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt; verifyBuilt
    for service in "${services[@]}"; do
      waitReady "eduivme-production-$service-1" "$service"
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
      docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
    done
    unchanged; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-registration-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
tag=coaching-registration-20261014
services=(identity-service admin-panel)
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
waitService() {
  local container=$1 service=$2 ip port=8080 path=/health/ready
  local -a headers=()
  if [[ "$service" == admin-panel ]]; then port=4000; path=/auth/register/student; headers=(-H 'Host: eduivme.com'); fi
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
    [[ "$container" == eduivme-production-identity-service-1 || "$container" == eduivme-production-admin-panel-1 ]] && continue
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $container" >&2; return 1; }
  done < previous-containers.txt
}
case "${1:-validate}" in
  prepare)
    test -s drill-verified.txt; test ! -e prepared.txt
    sha256sum -c payload.sha256 > payload-verified.log
    [[ $(docker inspect eduivme-production-identity-service-1 --format '{{.Image}}') == sha256:da3159ce4d40d86dbf392d2ebee409b47e842fd4442e6a8ba6a0dc7e7e2de2d7 ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:42c0a7299ba76303b8e368480d172da17c3a19e6edaae9d8dcb8b6a592be5f09 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' |
      while read -r container; do docker inspect "$container" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect eduivme-production-identity-service-1 eduivme-production-admin-panel-1 |
      jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
    files=("${compose[@]}")
    while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE
    TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
    "${files[@]}" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 previous-containers.txt) |
      jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > "$service.environment.json"
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json
      mv next.json frozen.compose.json
    done
    jq 'walk(if type == "string" then gsub("\\$"; "$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["identity-service"].image=("eduivme/identity-service:"+$tag) | .services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      dockerfile=identity.Dockerfile; [[ "$service" != admin-panel ]] || dockerfile=admin.Dockerfile
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    done
    candidates=()
    cleanup() { for candidate in "${candidates[@]}"; do docker rm -f "$candidate" >/dev/null 2>&1 || true; done; }
    trap cleanup EXIT
    for service in "${services[@]}"; do
      candidate="coaching-registration-preview-$service-20261014"
      docker inspect "$candidate" >/dev/null 2>&1 && { echo 'Preview container already exists' >&2; exit 2; }
      "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$candidate" "$service" > "$service-preview.log"
      candidates+=("$candidate")
      waitService "$candidate" "$service"
    done
    unchanged; date -u +%FT%TZ > preview-verified.txt
    ;;
  promote)
    test -s preview-verified.txt; test ! -e deployed.txt
    for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
    unchanged
    rollback() {
      local status=$?
      trap - EXIT
      if (( status != 0 )); then
        if ! "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1; then echo 'CRITICAL: rollback command failed' >&2; exit 70; fi
        for service in "${services[@]}"; do
          if ! waitService "eduivme-production-$service-1" "$service" || [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') != "$(<"previous-$service-image.txt")" ]]; then
            echo "CRITICAL: rollback image/health verification failed for $service" >&2; exit 70
          fi
        done
        echo 'Previous Identity and frontend images restored and verified' >&2
      fi
      exit "$status"
    }
    trap rollback EXIT
    for service in "${services[@]}"; do
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitService "eduivme-production-$service-1" "$service"
    done
    curl -fsS --max-time 15 https://eduivme.com/auth/register/student >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    for service in "${services[@]}"; do
      waitService "eduivme-production-$service-1" "$service"
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}')" ]]
      docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
    done
    unchanged; cat deployed.txt
    ;;
  *) echo 'Supported: prepare/build/promote/validate' >&2; exit 2 ;;
esac

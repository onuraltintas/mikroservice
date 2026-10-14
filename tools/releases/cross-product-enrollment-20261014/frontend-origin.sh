#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/cross-product-enrollment-origin-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
services=(admin-panel speed-reading-frontend)
tag=cross-product-enrollment-20261014-r2
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
ready() {
  local container=$1 service=$2 port=4000 path=/auth/register/student host=eduivme.com ip
  if [[ "$service" == speed-reading-frontend ]]; then port=80; path=/auth/register; host=masterhizliokuma.com; fi
  for attempt in $(seq 1 40); do
    [[ $(docker inspect "$container" --format '{{.State.Running}}') == true ]] || return 1
    ip=$(docker inspect "$container" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 -H "Host: $host" "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  return 1
}
unchanged() {
  while read -r container id; do
    [[ "$container" == eduivme-production-admin-panel-1 || "$container" == eduivme-production-speed-reading-frontend-1 ]] && continue
    [[ $(docker inspect "$container" --format '{{.Id}}') == "$id" ]] || return 1
  done < previous-containers.txt
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt
    sha256sum -c payload.sha256 > payload-verified.log
    cp ../cross-product-enrollment-20261014/production.compose.json previous.compose.json
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' |
      while read -r container; do docker inspect "$container" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(docker image inspect "eduivme/$service:cross-product-enrollment-20261014" --format '{{.Id}}')" ]]
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      dockerfile=admin.Dockerfile; [[ "$service" != speed-reading-frontend ]] || dockerfile=speed-reading.Dockerfile
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    done
    jq --arg tag "$tag" '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag)' previous.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    candidates=()
    cleanup() { for container in "${candidates[@]}"; do docker rm -f "$container" >/dev/null 2>&1 || true; done; }
    trap cleanup EXIT
    for service in "${services[@]}"; do
      candidate="cross-product-origin-preview-$service-20261014"
      docker inspect "$candidate" >/dev/null 2>&1 && exit 2
      "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$candidate" "$service" > "$service-preview.log"
      candidates+=("$candidate"); ready "$candidate" "$service"
    done
    unchanged; date -u +%FT%TZ > prepared.txt
    ;;
  promote)
    test -s prepared.txt; test ! -e deployed.txt
    for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f previous.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || exit 70
        for service in "${services[@]}"; do
          ready "eduivme-production-$service-1" "$service" || exit 70
          [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]] || exit 70
        done
      fi
      exit "$status"
    }
    trap rollback EXIT
    for service in "${services[@]}"; do
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1
      ready "eduivme-production-$service-1" "$service"
    done
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    for service in "${services[@]}"; do
      ready "eduivme-production-$service-1" "$service"
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}')" ]]
      docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
    done
    unchanged; cat deployed.txt
    ;;
  *) exit 2 ;;
esac

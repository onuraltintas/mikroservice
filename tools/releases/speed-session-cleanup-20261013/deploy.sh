#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/speed-session-cleanup-20261013
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 9 || { echo 'Another Speed Reading release is running' >&2; exit 2; }
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
api_image=eduivme/speed-reading-service:session-cleanup-319b250c
web_image=eduivme/speed-reading-frontend:session-cleanup-319b250c
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
base_compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
declare -A seen=()
export TEEN_API_IMAGE
TEEN_API_IMAGE=$(docker inspect "$backend" --format '{{.Config.Image}}')
for container in "$backend" "$frontend"; do
  files=$(docker inspect "$container" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
  IFS=',' read -r -a config_files <<< "$files"
  for file in "${config_files[@]}"; do
    test -f "$file"
    if [[ -z "${seen[$file]:-}" ]]; then base_compose+=(-f "$file"); seen[$file]=1; fi
  done
done
new_compose=("${base_compose[@]}" -f "$release/production.override.yml")
check_images() {
  for image in "$api_image" "$web_image"; do
    [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
  done
}
api_ready() {
  local ip
  ip=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "$backend" | awk '{print $1}')
  [[ "$ip" =~ ^[0-9.]+$ ]] || return 1
  [[ $(curl -fsS --connect-timeout 5 --max-time 10 "http://$ip:8080/health/ready") == Healthy ]]
}
wait_ready() {
  for attempt in $(seq 1 30); do
    if [[ $(docker inspect -f '{{.State.Running}}' "$backend" 2>/dev/null || true) == true ]] \
      && [[ $(docker inspect -f '{{.State.Health.Status}}' "$frontend" 2>/dev/null || true) == healthy ]] \
      && api_ready; then
      return 0
    fi
    sleep 2
  done
  return 1
}
case "${1:-validate}" in
  validate)
    sha256sum -c payload.sha256
    test -s api/SpeedReading.API.dll
    test -s web/browser/index.html
    "${new_compose[@]}" config --quiet
    ;;
  build)
    test ! -e previous-api-image.txt
    sha256sum -c payload.sha256
    docker inspect "$backend" --format '{{.Image}}' > previous-api-image.txt
    docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
    docker inspect "$backend" --format '{{.Config.Image}}' > previous-api-tag.txt
    docker inspect "$frontend" --format '{{.Config.Image}}' > previous-web-tag.txt
    mkdir -p previous/browser
    docker cp "$frontend:/usr/share/nginx/html/." previous/browser/
    docker build --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "$api_image" .
    docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$web_image" .
    check_images
    printf '%s\n' "$revision" > images-built.txt
    ;;
  promote)
    test "$(<images-built.txt)" = "$revision"
    check_images
    [[ $(docker inspect "$backend" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
    [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
    api_tag=$(<previous-api-tag.txt)
    web_tag=$(<previous-web-tag.txt)
    [[ "$api_tag" =~ ^[a-zA-Z0-9._/:@-]+$ && "$web_tag" =~ ^[a-zA-Z0-9._/:@-]+$ ]]
    printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$api_tag" "$web_tag" > previous.override.yml
    previous_compose=("${base_compose[@]}" -f "$release/previous.override.yml")
    "${new_compose[@]}" config --quiet
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    rollback_enabled=1
    on_exit() {
      status=$?
      if [[ "$status" != 0 && "${rollback_enabled:-0}" == 1 ]]; then
        echo 'Release health check failed; restoring the previous Speed Reading images.' >&2
        "${previous_compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend || \
          echo 'Automatic rollback failed; inspect the Speed Reading containers immediately.' >&2
      fi
      exit "$status"
    }
    trap on_exit EXIT
    "${new_compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
    wait_ready
    test "$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 20 https://masterhizliokuma.com/)" = 200
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    diff -u <(grep -Ev "^($backend|$frontend) " containers-before.txt) <(grep -Ev "^($backend|$frontend) " containers-after.txt)
    rollback_enabled=0
    date -u +%FT%TZ > deployed-at.txt
    trap - EXIT
    ;;
  rollback)
    test -s previous.override.yml
    previous_compose=("${base_compose[@]}" -f "$release/previous.override.yml")
    "${previous_compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
    wait_ready
    ;;
  *) echo 'Usage: bash deploy.sh {validate|build|promote|rollback}' >&2; exit 2 ;;
esac

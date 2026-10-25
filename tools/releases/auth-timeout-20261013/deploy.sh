#!/usr/bin/env bash
set -euo pipefail
umask 077

release=/var/lib/eduivme/releases/auth-timeout-20261013-319b250c-r1
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 9 || { echo 'Another Speed Reading release is running' >&2; exit 2; }

frontend=eduivme-production-speed-reading-frontend-1
api=eduivme-production-api-gateway-1
speedApi=eduivme-production-speed-reading-service-1
webImage=eduivme/speed-reading-frontend:auth-timeout-20261013-319b250c
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
releaseMarker=auth-timeout-20261013
export TEEN_API_IMAGE
TEEN_API_IMAGE=$(docker inspect "$speedApi" --format '{{.Config.Image}}')
baseCompose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
declare -A seen=()
files=$(docker inspect "$frontend" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -r -a configFiles <<< "$files"
for file in "${configFiles[@]}"; do
  test -f "$file"
  if [[ -z "${seen[$file]:-}" ]]; then baseCompose+=(-f "$file"); seen[$file]=1; fi
done
newCompose=("${baseCompose[@]}" -f "$release/production.override.yml")

checkImage() {
  [[ $(docker image inspect "$webImage" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
  [[ $(docker image inspect "$webImage" --format '{{index .Config.Labels "org.opencontainers.image.release"}}') == auth-timeout-20261013 ]]
}

waitFrontend() {
  for attempt in $(seq 1 30); do
    if [[ $(docker inspect -f '{{.State.Running}}' "$frontend" 2>/dev/null || true) == true ]] \
      && [[ $(docker inspect -f '{{.State.Health.Status}}' "$frontend" 2>/dev/null || true) == healthy ]]; then
      return 0
    fi
    sleep 2
  done
  return 1
}

verifyPublicFrontend() {
  local attempt loginStatus
  for attempt in $(seq 1 12); do
    loginStatus=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
      --header 'Cache-Control: no-cache' --max-time 20 \
      "https://masterhizliokuma.com/auth/login?release=$releaseMarker" 2>/dev/null || true)
    if [[ "$loginStatus" == 200 ]] && \
      curl --silent --show-error --fail --max-time 20 \
        --header 'Cache-Control: no-cache' \
        "https://masterhizliokuma.com/ngsw-worker.js?release=$releaseMarker" 2>/dev/null | \
        grep -F "$releaseMarker" >/dev/null; then
      return 0
    fi
    sleep 5
  done
  return 1
}

case "${1:-validate}" in
  validate)
    sha256sum -c payload.sha256
    test -s web/browser/index.html
    test -s web/browser/ngsw-worker.js
    test -s nginx.conf
    "${newCompose[@]}" config --quiet
    ;;
  build)
    sha256sum -c payload.sha256
    docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
    docker inspect "$frontend" --format '{{.Config.Image}}' > previous-web-tag.txt
    docker inspect "$speedApi" --format '{{.Image}}' > previous-speed-api-image.txt
    docker inspect "$api" --format '{{.Image}}' > previous-api-image.txt
    docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$webImage" .
    checkImage
    printf '%s\n' "$revision" > images-built.txt
    ;;
  promote)
    test "$(<images-built.txt)" = "$revision"
    checkImage
    [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
    [[ $(docker inspect "$speedApi" --format '{{.Image}}') == "$(<previous-speed-api-image.txt)" ]]
    [[ $(docker inspect "$api" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
    webTag=$(<previous-web-tag.txt)
    [[ "$webTag" =~ ^[a-zA-Z0-9._/:@-]+$ ]]
    printf 'services:\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$webTag" > previous.override.yml
    previousCompose=("${baseCompose[@]}" -f "$release/previous.override.yml")
    "${newCompose[@]}" config --quiet
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    rollbackEnabled=1
    onExit() {
      status=$?
      trap - EXIT
      if [[ "$status" != 0 && "${rollbackEnabled:-0}" == 1 ]]; then
        set +e
        echo 'Promotion failed; restoring only the Speed Reading frontend.' >&2
        "${previousCompose[@]}" up -d --no-deps --pull never speed-reading-frontend || \
          echo 'Frontend rollback failed; inspect the Speed Reading frontend immediately.' >&2
      fi
      exit "$status"
    }
    trap onExit EXIT
    "${newCompose[@]}" up -d --no-deps --pull never speed-reading-frontend
    waitFrontend
    docker exec "$frontend" sh -lc "grep -Fq '$releaseMarker' /usr/share/nginx/html/ngsw-worker.js"
    verifyPublicFrontend
    [[ $(docker inspect "$speedApi" --format '{{.Image}}') == "$(<previous-speed-api-image.txt)" ]]
    [[ $(docker inspect "$api" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    diff -u <(grep -Ev "^($frontend) " containers-before.txt) <(grep -Ev "^($frontend) " containers-after.txt)
    rollbackEnabled=0
    date -u +%FT%TZ > deployed-at.txt
    trap - EXIT
    ;;
  rollback)
    test -s previous.override.yml
    previousCompose=("${baseCompose[@]}" -f "$release/previous.override.yml")
    "${previousCompose[@]}" up -d --no-deps --pull never speed-reading-frontend
    waitFrontend
    ;;
  *) echo 'Usage: bash deploy.sh {validate|build|promote|rollback}' >&2; exit 2 ;;
esac

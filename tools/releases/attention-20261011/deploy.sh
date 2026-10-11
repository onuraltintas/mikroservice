#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/attention-20261011-catalogue
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another Speed Reading release is running' >&2; exit 2; }
frontend=eduivme-production-speed-reading-frontend-1
image=eduivme/speed-reading-frontend:attention-20261011-catalogue
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
files=$(docker inspect "$frontend" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -r -a config_files <<< "$files"
for file in "${config_files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
export TEEN_API_IMAGE
TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
case "${1:-validate}" in
  validate)
    sha256sum -c web.sha256
    "${compose[@]}" -f "$release/production.override.yml" config --quiet
    ;;
  build)
    test ! -e previous-image.txt
    docker inspect "$frontend" --format '{{.Config.Image}}' > previous-image.txt
    mkdir -p previous/browser
    docker cp "$frontend:/usr/share/nginx/html/." previous/browser/
    sha256sum -c web.sha256
    docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$image" .
    ;;
  promote)
    test ! -e deployed-at.txt
    [[ $(docker inspect "$frontend" --format '{{.Config.Image}}') == "$(<previous-image.txt)" ]]
    [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    "${compose[@]}" -f "$release/production.override.yml" config --quiet
    "${compose[@]}" -f "$release/production.override.yml" up -d --no-deps --pull never speed-reading-frontend
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    diff -u <(grep -v "^$frontend " containers-before.txt) <(grep -v "^$frontend " containers-after.txt)
    date -u +%FT%TZ > deployed-at.txt
    ;;
  rollback)
    previous_image=$(<previous-image.txt)
    docker image inspect "$previous_image" >/dev/null
    printf 'services:\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$previous_image" > rollback.override.yml
    "${compose[@]}" -f "$release/rollback.override.yml" config --quiet
    "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps --pull never speed-reading-frontend
    date -u +%FT%TZ > rolled-back-at.txt
    ;;
  *) exit 2 ;;
esac

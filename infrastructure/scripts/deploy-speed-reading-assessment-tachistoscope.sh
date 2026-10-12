#!/usr/bin/env bash
set -euo pipefail
umask 077
release=$PWD
[[ "$release" == /var/lib/eduivme/releases/assessment-tachistoscope-* ]] || exit 2
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]] || exit 2
web_image="eduivme/speed-reading-frontend:assessment-tachistoscope-${revision:0:8}"
frontend=eduivme-production-speed-reading-frontend-1
backend=eduivme-production-speed-reading-service-1
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another release is running' >&2; exit 2; }
prepare_compose() {
 compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
 export TEEN_API_IMAGE
 TEEN_API_IMAGE=$(docker inspect "$backend" --format '{{.Config.Image}}')
 files=$(docker inspect "$frontend" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
 IFS=',' read -r -a config_files <<< "$files"
 for file in "${config_files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
 compose+=(-f "$release/production.override.yml")
 "${compose[@]}" config --quiet
}
check_image() {
 [[ $(docker image inspect "$web_image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
}
ready() {
 for attempt in {1..20}; do
  if [[ $(docker inspect "$frontend" --format '{{.State.Health.Status}}') == healthy ]]; then return 0; fi
  sleep 2
 done
 return 1
}
case "${1:-build}" in
 build)
  test ! -e previous-web-image.txt
  docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
  printf 'services:\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$web_image" > production.override.yml
  printf 'services:\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$(<previous-web-image.txt)" > rollback.override.yml
  prepare_compose
  docker build --build-arg "PREVIOUS_WEB_IMAGE=$(<previous-web-image.txt)" --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$web_image" .
  check_image
  docker run --rm "$web_image" nginx -t
  ;;
 promote)
  test ! -e deployed-at.txt
  check_image; prepare_compose
  [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
  succeeded=false
  changed=false
  recover() {
   status=$?
   trap - EXIT INT TERM
   if [[ "$succeeded" != true && "$changed" == true ]]; then
    echo 'Restoring previous frontend image; API and database were not changed' >&2
    if ! "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps --pull never speed-reading-frontend || ! ready; then
     echo 'ROLLBACK FAILED: manual frontend recovery required' >&2
    fi
   fi
   exit "$status"
  }
  trap recover EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  changed=true
  "${compose[@]}" up -d --no-deps --pull never speed-reading-frontend
  ready
  curl -fsS --max-time 15 -H 'Cache-Control: no-cache' "https://masterhizliokuma.com/?release=$revision" > public-index.html
  cmp web/browser/index.html public-index.html
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
  diff -u <(grep -v "^$frontend " containers-before.txt) <(grep -v "^$frontend " containers-after.txt)
  date -u +%FT%TZ > deployed-at.txt
  succeeded=true
  trap - EXIT INT TERM
  ;;
 *) exit 2 ;;
esac

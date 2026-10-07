#!/usr/bin/env bash
set -euo pipefail
umask 077
cd "$(dirname "$0")"
release=$PWD
tag="exercises-20261007-$(cat revision.txt)"
case "${1:-}" in
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-speed-reading-frontend-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  printf 'services:\n' > rollback.override.yml
  printf 'services:\n' > production.override.yml
  for service in speed-reading-service speed-reading-frontend; do
    previous=$(docker inspect "eduivme-production-$service-1" --format '{{.Image}}')
    printf '  %s:\n    image: %s\n    pull_policy: never\n' "$service" "$previous" >> rollback.override.yml
    printf '  %s:\n    image: eduivme/%s:%s\n    pull_policy: never\n' "$service" "$service" "$tag" >> production.override.yml
  done
  ;;
build)
  docker build -t "eduivme/speed-reading-service:$tag" -f source/services/speed-reading-service/Dockerfile source > backend-build.log 2>&1
  docker build --build-arg "RELEASE_TAG=$tag" -t "eduivme/speed-reading-frontend:$tag" -f source/clients/speed-reading/Dockerfile source/clients > frontend-build.log 2>&1
  ;;
deploy|rollback)
  override=production.override.yml
  if [[ "$1" == rollback ]]; then override=rollback.override.yml; fi
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  compose+=(-f "$release/$override")
  "${compose[@]}" config --quiet
  "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
  ;;
smoke)
  for service in speed-reading-service speed-reading-frontend; do
    test "$(docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}}')" = "eduivme/$service:$tag"
    test "$(docker inspect "eduivme-production-$service-1" --format '{{.State.Running}}')" = true
  done
  test "$(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.State.Health.Status}}')" = healthy
  for path in / /student/exercises /student/training-programs /ngsw-worker.js; do
    curl --fail --silent --show-error --retry 5 --max-time 20 "https://masterhizliokuma.com$path" -o /dev/null
  done
  curl --fail --silent --show-error --max-time 20 https://masterhizliokuma.com/ngsw-worker.js | grep "release: $tag" > /dev/null
  printf 'Public SPA, container health and release worker OK\n'
  ;;
*) exit 2 ;;
esac

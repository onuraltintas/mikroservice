#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/custom-preview-levels-20261005
tag=custom-preview-levels-20261005-383643f1
cd "$release"
case "${1:-}" in
build)
  docker build --build-arg "RELEASE_TAG=$tag" -t "eduivme/speed-reading-frontend:$tag" -f source/clients/speed-reading/Dockerfile source/clients > build.log 2>&1
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-speed-reading-frontend-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  previous=$(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.Image}}')
  printf 'services:\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$previous" > rollback.override.yml
  printf 'services:\n  speed-reading-frontend:\n    image: eduivme/speed-reading-frontend:%s\n    pull_policy: never\n' "$tag" > production.override.yml
  ;;
deploy|rollback)
  override=production.override.yml
  if [[ "$1" == rollback ]]; then override=rollback.override.yml; fi
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  compose+=(-f "$release/$override")
  "${compose[@]}" config --quiet
  "${compose[@]}" up -d --no-deps --pull never speed-reading-frontend
  ;;
smoke)
  test "$(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.Config.Image}}')" = "eduivme/speed-reading-frontend:$tag"
  test "$(docker inspect eduivme-production-speed-reading-frontend-1 --format '{{.State.Health.Status}}')" = healthy
  for path in / /student/exercises /student/training-programs /ngsw-worker.js; do
    curl --fail --silent --show-error --retry 5 --max-time 20 "https://masterhizliokuma.com$path" -o /dev/null
  done
  curl --fail --silent --show-error --max-time 20 https://masterhizliokuma.com/ngsw-worker.js | grep -q "release: $tag"
  printf 'Public SPA, container health and release worker OK\n'
  ;;
*) exit 2 ;;
esac

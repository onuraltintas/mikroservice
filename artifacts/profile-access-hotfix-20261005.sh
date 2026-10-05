#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/profile-access-20261005
tag=profile-access-20261005
cd "$release"
case "${1:-}" in
build)
  docker build -t "eduivme/speed-reading-service:$tag" -f source/services/speed-reading-service/Dockerfile source > build.log 2>&1
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-speed-reading-service-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  previous=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Image}}')
  printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n' "$previous" > rollback.override.yml
  printf 'services:\n  speed-reading-service:\n    image: eduivme/speed-reading-service:%s\n    pull_policy: never\n' "$tag" > production.override.yml
  ;;
deploy|rollback)
  override=production.override.yml
  if [[ "$1" == rollback ]]; then override=rollback.override.yml; fi
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  compose+=(-f "$release/$override")
  "${compose[@]}" config --quiet
  "${compose[@]}" up -d --no-deps --pull never speed-reading-service
  ;;
smoke)
  container=eduivme-production-speed-reading-service-1
  test "$(docker inspect "$container" --format '{{.Config.Image}}')" = "eduivme/speed-reading-service:$tag"
  ip=$(docker inspect "$container" | jq -er '.[0].NetworkSettings.Networks["eduplatform-production"].IPAddress')
  curl --fail --silent --show-error --retry 10 --retry-connrefused --retry-delay 3 --max-time 10 "http://$ip:8080/health/ready"
  test "$(curl --silent --show-error --max-time 20 -o /dev/null -w '%{http_code}' https://masterhizliokuma.com/api/speed-reading/adaptive-learning/profile/settings)" = 401
  printf '\nReadiness OK; anonymous profile settings HTTP401\n'
  ;;
*) exit 2 ;;
esac

#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/eft-validation-20261005
tag=eft-validation-20261005
cd "$release"
services=(coaching-service speed-reading-service)
case "${1:-}" in
build)
  for service in "${services[@]}"; do
    docker build -t "eduivme/$service:$tag" -f "source/services/$service/Dockerfile" source > "$service-build.log" 2>&1
  done
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  printf 'services:\n' > production.override.yml
  printf 'services:\n' > rollback.override.yml
  for service in "${services[@]}"; do
    previous=$(docker inspect "eduivme-production-$service-1" --format '{{.Image}}')
    printf '  %s:\n    image: %s\n    pull_policy: never\n' "$service" "$previous" >> rollback.override.yml
    printf '  %s:\n    image: eduivme/%s:%s\n    pull_policy: never\n' "$service" "$service" "$tag" >> production.override.yml
  done
  ;;
deploy|rollback)
  override=production.override.yml
  if [[ "$1" == rollback ]]; then override=rollback.override.yml; fi
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  compose+=(-f "$release/$override")
  "${compose[@]}" config --quiet
  "${compose[@]}" up -d --no-deps --pull never "${services[@]}"
  ;;
smoke)
  for service in "${services[@]}"; do
    container="eduivme-production-$service-1"
    test "$(docker inspect "$container" --format '{{.Config.Image}}')" = "eduivme/$service:$tag"
    test "$(docker inspect "$container" --format '{{.State.Running}}')" = true
    ip=$(docker inspect "$container" | jq -er '.[0].NetworkSettings.Networks["eduplatform-production"].IPAddress')
    curl --fail --silent --show-error --retry 10 --retry-connrefused --retry-delay 3 --max-time 10 "http://$ip:8080/health/ready"
    printf '\n%s ready\n' "$service"
  done
  for url in https://masterhizliokuma.com/ https://onuraltintas.net/; do
    test "$(curl --silent --show-error --max-time 20 -o /dev/null -w '%{http_code}' "$url")" = 200
    printf '%s HTTP200\n' "$url"
  done
  ;;
*) exit 2 ;;
esac

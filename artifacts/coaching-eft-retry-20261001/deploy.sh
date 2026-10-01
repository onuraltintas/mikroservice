#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/coaching-eft-retry-20261001
cd "$release"
files=$(docker inspect eduivme-production-coaching-service-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -ra config_files <<< "$files"
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
for file in "${config_files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
case "${1:-inspect}" in
prepare)
  test ! -e production.override.yml
  previous=$(docker inspect eduivme-production-coaching-service-1 --format '{{.Config.Image}}')
  printf 'services:\n  coaching-service:\n    image: %s\n' "$previous" > rollback.override.yml
  printf 'services:\n  coaching-service:\n    image: eduivme/coaching-service:eft-retry-20261001\n' > production.override.yml
  chmod 600 *.override.yml
  "${compose[@]}" -f "$release/production.override.yml" config --quiet
  ;;
build) docker build -t eduivme/coaching-service:eft-retry-20261001 . ;;
deploy) "${compose[@]}" -f "$release/production.override.yml" up -d --no-deps coaching-service ;;
rollback) "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps coaching-service ;;
*) exit 2 ;;
esac

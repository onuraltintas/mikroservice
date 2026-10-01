#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/coaching-central-consent-20261001
cd "$release"
services=(coaching-service admin-panel)
files=$(docker inspect eduivme-production-admin-panel-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -ra config_files <<< "$files"
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
for file in "${config_files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
case "${1:-inspect}" in
prepare)
  test ! -e production.override.yml
  printf 'services:\n' > production.override.yml
  printf 'services:\n' > rollback.override.yml
  for service in "${services[@]}"; do
    previous=$(docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}}')
    printf '  %s:\n    image: %s\n' "$service" "$previous" >> rollback.override.yml
    printf '  %s:\n    image: eduivme/%s:coaching-central-consent-20261001\n' "$service" "$service" >> production.override.yml
  done
  chmod 600 *.override.yml
  "${compose[@]}" -f "$release/production.override.yml" config --quiet
  ;;
build)
  docker build -f Dockerfile.coaching -t eduivme/coaching-service:coaching-central-consent-20261001 .
  docker build -f Dockerfile.admin -t eduivme/admin-panel:coaching-central-consent-20261001 .
  ;;
deploy) "${compose[@]}" -f "$release/production.override.yml" up -d --no-deps "${services[@]}" ;;
rollback) "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps "${services[@]}" ;;
*) exit 2 ;;
esac

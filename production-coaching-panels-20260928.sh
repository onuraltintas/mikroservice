#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/coaching-panels-20260928-697bdac5
files=$(docker inspect eduivme-production-identity-service-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -r -a config_files <<< "$files"
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
for file in "${config_files[@]}"; do
  test -f "$file"
  compose+=(-f "$file")
done
compose+=(-f "$release/production.override.yml")
case "${1:-validate}" in
  validate)
    "${compose[@]}" config --quiet
    ;;
  promote)
    "${compose[@]}" up -d --no-deps identity-service coaching-service admin-panel
    ;;
  rollback)
    compose+=(-f /var/lib/eduivme/releases/platform-release-20260928-2cedc8f0/production.override.yml)
    "${compose[@]}" up -d --no-deps identity-service coaching-service admin-panel
    ;;
  *) exit 2 ;;
esac

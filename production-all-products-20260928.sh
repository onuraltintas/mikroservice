#!/usr/bin/env bash
set -euo pipefail

release=/var/lib/eduivme/releases/all-products-20260928-265a6d2c-wt
override="$release/production.override.yml"
rollback_override="$release/rollback.override.yml"
files=$(docker inspect eduivme-production-identity-service-1 \
  --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
IFS=',' read -r -a config_files <<< "$files"
compose=(docker compose --project-name eduivme-production \
  --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
for file in "${config_files[@]}"; do
  test -f "$file"
  compose+=(-f "$file")
done

case "${1:-validate}" in
  validate)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    "${compose[@]}" config --quiet
    ;;
  migrate)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    for service in identity-migrations coaching-migrations speed-reading-migrations; do
      "${compose[@]}" run --rm --no-deps "$service"
    done
    ;;
  backends)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    "${compose[@]}" up -d --no-deps identity-service coaching-service speed-reading-service
    ;;
  gateway)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    "${compose[@]}" up -d --no-deps api-gateway
    ;;
  frontends)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    "${compose[@]}" up -d --no-deps admin-panel speed-reading-frontend
    ;;
  rollback)
    if [[ ",$files," != *",$override,"* ]]; then compose+=(-f "$override"); fi
    compose+=(-f "$rollback_override")
    "${compose[@]}" up -d --no-deps identity-service coaching-service \
      speed-reading-service api-gateway admin-panel speed-reading-frontend
    ;;
  *)
    echo 'Usage: production-all-products-20260928.sh {validate|migrate|backends|gateway|frontends|rollback}' >&2
    exit 2
    ;;
esac

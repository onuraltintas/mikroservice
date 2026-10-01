#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/staff-training-20261001
cd "$release"
services=(speed-reading-service speed-reading-frontend staff-portal)
files=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
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
    printf '  %s:\n    image: eduivme/%s:staff-training-20261001\n' "$service" "$service" >> production.override.yml
  done
  printf '  speed-reading-migrations:\n    image: eduivme/speed-reading-service:staff-training-20261001\n' >> production.override.yml
  chmod 600 *.override.yml
  mkdir -p /var/lib/eduivme/backups/staff-training-20261001
  chmod 700 /var/lib/eduivme/backups/staff-training-20261001
  docker exec postgres sh -c 'exec pg_dump -U "$POSTGRES_USER" -Fc speedreading_owned_db' > /var/lib/eduivme/backups/staff-training-20261001/speedreading_owned_db.dump
  test -s /var/lib/eduivme/backups/staff-training-20261001/speedreading_owned_db.dump
  for pair in 'speed-reading-frontend speed-nginx.conf' 'staff-portal staff-nginx.conf'; do
    read -r service target <<< "$pair"
    docker cp "eduivme-production-$service-1:/etc/nginx/conf.d/default.conf" "$target"
  done
  "${compose[@]}" -f "$release/production.override.yml" config --quiet
  ;;
build)
  docker build -f Dockerfile.api -t eduivme/speed-reading-service:staff-training-20261001 .
  docker build -f Dockerfile.frontend -t eduivme/speed-reading-frontend:staff-training-20261001 .
  docker build -f Dockerfile.staff -t eduivme/staff-portal:staff-training-20261001 .
  ;;
migrate) "${compose[@]}" -f "$release/production.override.yml" run --rm --no-deps speed-reading-migrations --migrate-only ;;
deploy) "${compose[@]}" -f "$release/production.override.yml" up -d --no-deps "${services[@]}" ;;
rollback) "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps "${services[@]}" ;;
*) exit 2 ;;
esac

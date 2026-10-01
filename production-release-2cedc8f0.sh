#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/platform-release-20260928-2cedc8f0
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
  migrate)
    for service in identity-migrations coaching-migrations notification-migrations; do
      "${compose[@]}" run --rm --no-deps "$service"
    done
    migration_user=$(docker exec postgres printenv POSTGRES_USER)
    migration_password=$(docker exec postgres printenv POSTGRES_PASSWORD)
    "${compose[@]}" run --rm --no-deps \
      -e "ConnectionStrings__SpeedReadingOwned=Host=postgres;Port=5432;Database=speedreading_owned_db;Username=$migration_user;Password=$migration_password" \
      speed-reading-migrations
    runtime_role=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | grep '^ConnectionStrings__SpeedReadingOwned=' | sed -n 's/.*Username=\([^;]*\).*/\1/p')
    [[ "$runtime_role" =~ ^[a-zA-Z0-9_]+$ ]]
    docker exec postgres sh -lc 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db -c "$1"' sh \
      "GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE speed_reading.review_completions TO \"$runtime_role\";"
    ;;
  promote)
    "${compose[@]}" up -d --no-deps identity-service coaching-service notification-service speed-reading-service api-gateway admin-panel speed-reading-frontend
    ;;
  *) exit 2 ;;
esac

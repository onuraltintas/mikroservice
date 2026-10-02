#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/coaching-planning-0d604256
cd "$release"
case "${1:-}" in
catalog-import|catalog-publish)
  test -f migrations-complete
  export COACHING_CATALOG_CONNECTION
  COACHING_CATALOG_CONNECTION=$(docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")')
  [[ "$COACHING_CATALOG_CONNECTION" == *"Host=postgres;"* ]]
  [[ "$COACHING_CATALOG_CONNECTION" == *"Database=coaching_db;"* ]]
  action=import
  extra=()
  if [ "$1" = catalog-publish ]; then action=publish; extra+=(--publication-authorized); fi
  docker run --rm --network eduplatform-production --env COACHING_CATALOG_CONNECTION \
    -v "$release/catalog:/catalog:ro" -v "$release/catalog-tool:/catalog-tool:ro" \
    -w /catalog-tool --entrypoint dotnet \
    eduivme/coaching-service:planning-0d604256 Coaching.Catalog.dll \
    --directory /catalog --source approved-catalog --action "$action" \
    --apply --database coaching_db "${extra[@]}"
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-coaching-service-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}' > compose-files.txt
  printf 'services:\n' > rollback.override.yml
  for service in coaching-service notification-service admin-panel; do
    previous=$(docker inspect "eduivme-production-${service}-1" --format '{{.Config.Image}}')
    printf '  %s:\n    image: %s\n' "$service" "$previous" >> rollback.override.yml
  done
  printf 'services:\n  coaching-service:\n    image: eduivme/coaching-service:planning-0d604256\n  notification-service:\n    image: eduivme/notification-service:planning-0d604256\n  admin-panel:\n    image: eduivme/admin-panel:planning-0d604256\n' > production.override.yml
  chmod 600 compose-files.txt *.override.yml
  ;;
migrate|deploy|rollback|validate)
  IFS=',' read -ra config_files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${config_files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  if [ "$1" = rollback ]; then
    # Never restore the old Notification worker: it cannot enforce erased-recipient guards.
    "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps coaching-service admin-panel
  else
    compose+=(-f "$release/production.override.yml")
    "${compose[@]}" config --quiet
    case "$1" in
      migrate)
        rm -f "$release/migrations-complete"
        "${compose[@]}" run --rm --no-deps coaching-service --migrate-only
        "${compose[@]}" run --rm --no-deps notification-service --migrate-only
        touch migrations-complete
        ;;
      deploy)
        test -f migrations-complete
        "${compose[@]}" up -d --no-deps coaching-service notification-service admin-panel
        ;;
    esac
  fi
  ;;
*) exit 2 ;;
esac

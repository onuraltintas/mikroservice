#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/legal-payment-20261005
tag=legal-payment-20261005
services=(coaching-service speed-reading-service admin-panel staff-portal speed-reading-frontend)
cd "$release"
db_user=$(docker exec postgres printenv POSTGRES_USER)

compose_command() {
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  compose+=(-f "$release/$1")
  "${compose[@]}" config --quiet
}

case "${1:-}" in
build)
  docker build -t "eduivme/coaching-service:$tag" -f source/services/coaching-service/Dockerfile source > coaching-build.log 2>&1
  docker build -t "eduivme/speed-reading-service:$tag" -f source/services/speed-reading-service/Dockerfile source > speed-build.log 2>&1
  docker build -t "eduivme/admin-panel:$tag" -f source/clients/admin-panel/Dockerfile source/clients > admin-build.log 2>&1
  docker build -t "eduivme/staff-portal:$tag" -f source/clients/admin-panel/Dockerfile.staff-portal source/clients > staff-build.log 2>&1
  docker build --build-arg "RELEASE_TAG=$tag" -t "eduivme/speed-reading-frontend:$tag" -f source/clients/speed-reading/Dockerfile source/clients > speed-ui-build.log 2>&1
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  printf 'services:\n' > production.override.yml
  printf 'services:\n' > rollback.override.yml
  for service in "${services[@]}"; do
    previous=$(docker inspect "eduivme-production-${service}-1" --format '{{.Image}}')
    printf '  %s:\n    image: %s\n    pull_policy: never\n' "$service" "$previous" >> rollback.override.yml
    printf '  %s:\n    image: eduivme/%s:%s\n    pull_policy: never\n' "$service" "$service" "$tag" >> production.override.yml
  done
  compose_command production.override.yml
  ;;
backup-drill|backup-drill-speed|repair-speed-drill)
  test ! -e drill-complete
  products=(coaching speed_reading)
  if [[ "$1" == backup-drill-speed || "$1" == repair-speed-drill ]]; then
    test -s coaching_db.backup
    test "$(docker exec postgres psql -U "$db_user" -d legal_payment_coaching_drill_20261005 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId"=$$20261005090416_RecordAdultPayerDeclaration$$')" = 1
    products=(speed_reading)
  fi
  for product in "${products[@]}"; do
    database="${product}_db"
    if [[ "$product" == speed_reading ]]; then database=speedreading_owned_db; fi
    drill="legal_payment_${product}_drill_20261005"
    if [[ "$1" == repair-speed-drill ]]; then
      test "$drill" = legal_payment_speed_reading_drill_20261005
      test -s "$database.backup"
      docker exec postgres dropdb -U "$db_user" "$drill"
    else
      test ! -e "$database.backup"
      docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.backup"
    fi
    test -s "$database.backup"
    docker exec -i postgres pg_restore --list < "$database.backup" > "$database.manifest"
    docker exec postgres createdb -U "$db_user" "$drill"
    # Preserve the source schema/table owners so the application role has the same migration rights.
    docker exec -i postgres pg_restore -U "$db_user" -d "$drill" --exit-on-error < "$database.backup"
    service=coaching-service
    migration=20261005090416_RecordAdultPayerDeclaration
    table=bank_transfer_requests
    connection_key=ConnectionStrings__DefaultConnection
    if [[ "$product" == speed_reading ]]; then
      service=speed-reading-service
      migration=20261005091502_RecordAdultPayerDeclaration
      table=bank_transfer_payment_requests
      connection_key=ConnectionStrings__SpeedReadingOwned
    fi
    connection=$(docker inspect "eduivme-production-${service}-1" | jq -er --arg prefix "$connection_key=" '.[0].Config.Env[] | select(startswith($prefix)) | ltrimstr($prefix)')
    [[ "$connection" == *"Database=$database;"* ]]
    printf -v "$connection_key" '%s' "${connection/Database=$database;/Database=$drill;}"
    export "$connection_key"
    docker run --rm --network eduplatform-production --env "$connection_key" "eduivme/$service:$tag" --migrate-only > "$product-drill.log" 2>&1
    unset "$connection_key" connection
    test "$(docker exec postgres psql -U "$db_user" -d "$drill" -Atc "SELECT count(*) FROM $product.__ef_migrations_history WHERE \"MigrationId\"='$migration'")" = 1
    test "$(docker exec postgres psql -U "$db_user" -d "$drill" -Atc "SELECT count(*) FROM $product.$table WHERE \"AdultPayerDeclarationVersion\" IS NOT NULL OR \"AdultPayerDeclaredAt\" IS NOT NULL")" = 0
  done
  for service in coaching-service speed-reading-service; do docker image inspect "eduivme/$service:$tag" --format '{{.Id}}'; done > drill-complete
  ;;
migrate|deploy|rollback)
  if [[ "$1" != rollback ]]; then
    test -s drill-complete
    diff drill-complete <(for service in coaching-service speed-reading-service; do docker image inspect "eduivme/$service:$tag" --format '{{.Id}}'; done)
  fi
  override=production.override.yml
  if [[ "$1" == rollback ]]; then override=rollback.override.yml; fi
  compose_command "$override"
  if [[ "$1" == migrate ]]; then
    for database in coaching_db speedreading_owned_db; do
      test ! -e "$database.production-before-migration.backup"
      docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.production-before-migration.backup"
      test -s "$database.production-before-migration.backup"
      docker exec -i postgres pg_restore --list < "$database.production-before-migration.backup" > "$database.production-before-migration.manifest"
    done
    "${compose[@]}" run --rm --no-deps coaching-service --migrate-only > coaching-production-migration.log 2>&1
    "${compose[@]}" run --rm --no-deps speed-reading-service --migrate-only > speed-production-migration.log 2>&1
    cp drill-complete migrations-complete
  elif [[ "$1" == deploy ]]; then
    diff drill-complete migrations-complete
    "${compose[@]}" up -d --no-deps "${services[@]}"
  else
    # Additive nullable columns remain; restoring old application images does not erase new evidence.
    "${compose[@]}" up -d --no-deps "${services[@]}"
  fi
  ;;
cleanup-drill)
  test -s migrations-complete
  docker exec postgres dropdb -U "$db_user" legal_payment_coaching_drill_20261005
  docker exec postgres dropdb -U "$db_user" legal_payment_speed_reading_drill_20261005
  ;;
*) exit 2 ;;
esac

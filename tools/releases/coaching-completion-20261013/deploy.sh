#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-completion-20261013-ac014834
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9 || { echo 'Another coaching deployment is running' >&2; exit 2; }
tag=coaching-completion-20261013-ac014834
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
services=(identity-service notification-service coaching-service admin-panel staff-portal)
db_user=$(docker exec postgres printenv POSTGRES_USER)
network=eduplatform-production
clone_prefix=coaching_completion_drill_20261013
declare -A databases=([identity-service]=identity_db [notification-service]=notification_db [coaching-service]=coaching_db)
declare -A roles=([identity-service]=identity [notification-service]=notification [coaching-service]=coaching)

baseCompose() {
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  while IFS= read -r file; do test -f "$file"; compose+=(-f "$file"); done < compose-files.txt
  export TEEN_API_IMAGE
  TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
}
verifyUnchangedTargets() {
  local container_prefix=previous
  if test -s rollback-verified.txt; then container_prefix=retry; fi
  for service in "${services[@]}"; do
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]]
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"$container_prefix-$service-container.txt")" ]]
  done
}
checkImages() {
  for service in "${services[@]}"; do
    [[ $(docker image inspect "eduivme/$service:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
  done
}
checkCoachingHistory() {
  local database=$1 output=$2
  test -s coaching-baseline-history.txt
  # Retain the documented historical production migration alongside repository migrations.
  comm -23 coaching-baseline-history.txt expected-coaching-migrations.txt > coaching-legacy-history.txt
  if test -s coaching-legacy-history.txt; then
    [[ $(<coaching-legacy-history.txt) == 20260924152005_AddAcademicGoalInstitutionScope ]]
  fi
  cat expected-coaching-migrations.txt coaching-legacy-history.txt | sort -u > expected-production-coaching-history.txt
  docker exec postgres psql -U "$db_user" -d "$database" -Atc 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1' > "$output"
  diff -u expected-production-coaching-history.txt "$output"
}
migrateClone() {
  local service=$1 database=${databases[$1]} role=${roles[$1]} clone="$clone_prefix"_${roles[$1]} connection
  test -s "$database.backup"
  [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$clone'") == 0 ]]
  docker exec postgres createdb -U "$db_user" "$clone"
  docker exec -i postgres pg_restore -U "$db_user" -d "$clone" --exit-on-error --no-owner --no-privileges < "$database.backup" > "$role-restore.log" 2>&1
  connection=$(docker inspect "eduivme-production-$service-1" | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")')
  [[ "$connection" == *"Database=$database;"* ]]
  export ConnectionStrings__DefaultConnection
  ConnectionStrings__DefaultConnection=${connection/Database=$database;/Database=$clone;}
  docker run --rm --network "$network" --env ConnectionStrings__DefaultConnection "eduivme/$service:$tag" --migrate-only > "$role-drill-migrations.log" 2>&1
  if [[ "$role" == coaching ]]; then
    checkCoachingHistory "$clone" coaching-drill-history.txt
    docker exec postgres psql -U "$db_user" -d "$clone" -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.fee_agreements; SELECT count(*) FROM coaching.reminder_receipts; SELECT count(*) FROM pg_trigger WHERE tgname IN ($$lock_session_scheduling$$,$$guard_session_overlap$$);' > coaching-drill-schema.txt
  fi
}
waitService() {
  local service=$1 port=$2 path=$3 ip
  local -a headers=()
  if [[ "$service" == admin-panel ]]; then headers=(-H 'Host: eduivme.com'); fi
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 "${headers[@]}" "http://$ip:$port$path" >/dev/null; then return 0; fi
    sleep 2
  done
  return 1
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt
    sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
    done
    docker inspect eduivme-production-identity-service-1 eduivme-production-notification-service-1 eduivme-production-coaching-service-1 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 |
      jq -r '.[].Config.Labels["com.docker.compose.project.config_files"] | split(",")[]' | awk '!seen[$0]++' > compose-files.txt
    # Pin every running product image and retain the target services' actual environment.
    docker inspect $(docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}') |
      jq '{services: (map({key: .Config.Labels["com.docker.compose.service"], value: {image: .Image, pull_policy: "never"}}) | from_entries)}' > rollback.override.json
    cp rollback.override.json production.override.json
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > "$service.environment.json"
      jq --arg service "$service" --arg image "eduivme/$service:$tag" --slurpfile env "$service.environment.json" '.services[$service].image=$image | .services[$service].environment=($env[0] | with_entries(.value |= gsub("\\$"; "$$")))' production.override.json > next.override.json
      mv next.override.json production.override.json
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=($env[0] | with_entries(.value |= gsub("\\$"; "$$")))' rollback.override.json > next.override.json
      mv next.override.json rollback.override.json
    done
    jq '.services["coaching-service"].environment.Coaching__Reminders__Enabled="false"' production.override.json > next.override.json
    mv next.override.json production.override.json
    baseCompose
    "${compose[@]}" -f production.override.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; verifyUnchangedTargets; sha256sum -c payload.sha256 > payload-verified.log
    for service in "${services[@]}"; do
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      [[ $(docker image inspect "eduivme/$service:$tag-base" --format '{{.Id}}') == "$(<"previous-$service-image.txt")" ]]
    done
    for service in identity-service notification-service coaching-service; do
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SERVICE=${roles[$service]}" --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
    done
    docker build --network none --build-arg "BASE_IMAGE=eduivme/admin-panel:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f admin.Dockerfile -t "eduivme/admin-panel:$tag" . > admin-build.log 2>&1
    docker build --network none --build-arg "BASE_IMAGE=eduivme/staff-portal:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f staff.Dockerfile -t "eduivme/staff-portal:$tag" . > staff-build.log 2>&1
    checkImages; date -u +%FT%TZ > built.txt
    ;;
  backup-drill)
    test -s built.txt; checkImages; verifyUnchangedTargets
    docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1' > coaching-baseline-history.txt
    for service in identity-service notification-service coaching-service; do
      database=${databases[$service]}; test ! -e "$database.backup"
      docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.backup"
      test -s "$database.backup"; docker exec -i postgres pg_restore --list < "$database.backup" > "$database.backup-manifest.txt"
      migrateClone "$service"
    done
    sha256sum *_db.backup > backups.sha256
    date -u +%FT%TZ > drill-complete.txt
    ;;
  verify-drill)
    test -s built.txt; checkImages; verifyUnchangedTargets
    test ! -e migrated.txt
    # Resume verification after the history comparison stopped the first clone rehearsal.
    if test ! -s coaching-baseline-history.txt; then
      docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1' > coaching-baseline-history.txt
    fi
    for service in identity-service notification-service coaching-service; do
      database=${databases[$service]}; test -s "$database.backup"
      docker exec -i postgres pg_restore --list < "$database.backup" > "$database.backup-manifest.txt"
      clone="${clone_prefix}_${roles[$service]}"
      [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$clone'") == 1 ]]
    done
    checkCoachingHistory "${clone_prefix}_coaching" coaching-drill-history.txt
    docker exec postgres psql -U "$db_user" -d "${clone_prefix}_coaching" -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.fee_agreements; SELECT count(*) FROM coaching.reminder_receipts; SELECT count(*) FROM pg_trigger WHERE tgname IN ($$lock_session_scheduling$$,$$guard_session_overlap$$);' > coaching-drill-schema.txt
    sha256sum *_db.backup > backups.sha256
    date -u +%FT%TZ > drill-complete.txt
    ;;
  migrate)
    test -s drill-complete.txt; checkImages; verifyUnchangedTargets; sha256sum -c backups.sha256 > backups-verified.log
    baseCompose
    # Dependency order: Identity contract, Notification storage, then Coaching schema.
    for service in identity-service notification-service coaching-service; do
      "${compose[@]}" -f production.override.json run --rm --no-deps --pull never "$service" --migrate-only > "$service-production-migrations.log" 2>&1
    done
    checkCoachingHistory coaching_db coaching-production-history.txt
    date -u +%FT%TZ > migrated.txt
    ;;
  verify-rollback)
    test -s migrated.txt; test -s rollback.log; test ! -e deployed.txt
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]]
    done
    for service in identity-service notification-service coaching-service; do waitService "$service" 8080 /health/ready; done
    waitService admin-panel 4000 /auth/login
    waitService staff-portal 80 /health/live
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "retry-$service-container.txt"
    done
    date -u +%FT%TZ > rollback-verified.txt
    ;;
  promote)
    test -s migrated.txt; checkImages; verifyUnchangedTargets; baseCompose
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    onExit() {
      status=$?; trap - EXIT
      if [[ "$status" != 0 ]]; then
        echo 'Release checks failed; restoring only the five target images. Schema Down is not applied.' >&2
        rollback_failed=0
        if ! "${compose[@]}" -f rollback.override.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1; then
          echo 'CRITICAL: image rollback command failed; inspect rollback.log and restore the pinned targets immediately.' >&2
          rollback_failed=1
        fi
        for service in "${services[@]}"; do
          if [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}' 2>/dev/null || true) != "$(<"previous-$service-image.txt")" ]]; then
            echo "CRITICAL: $service did not return to its previous image." >&2
            rollback_failed=1
          fi
        done
        for service in identity-service notification-service coaching-service; do
          if ! waitService "$service" 8080 /health/ready; then echo "CRITICAL: rolled-back $service is not ready." >&2; rollback_failed=1; fi
        done
        if ! waitService admin-panel 4000 /auth/login; then echo 'CRITICAL: rolled-back admin panel is not ready.' >&2; rollback_failed=1; fi
        if ! waitService staff-portal 80 /health/live; then echo 'CRITICAL: rolled-back staff portal is not ready.' >&2; rollback_failed=1; fi
        if [[ "$rollback_failed" != 0 ]]; then exit 70; fi
        echo 'Previous target images and readiness were verified after rollback.' >&2
      fi
      exit "$status"
    }
    trap onExit EXIT
    "${compose[@]}" -f production.override.json up -d --no-deps --pull never identity-service
    waitService identity-service 8080 /health/ready
    "${compose[@]}" -f production.override.json up -d --no-deps --pull never notification-service
    waitService notification-service 8080 /health/ready
    "${compose[@]}" -f production.override.json up -d --no-deps --pull never coaching-service
    waitService coaching-service 8080 /health/ready
    "${compose[@]}" -f production.override.json up -d --no-deps --pull never admin-panel staff-portal
    waitService admin-panel 4000 /auth/login
    waitService staff-portal 80 /health/live
    for url in https://onuraltintas.net/coaching-portal/finance https://onuraltintas.net/staff/?product=coaching https://eduivme.com/dashboard/coaching/finance; do
      [[ $(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' "$url") == 200 ]]
    done
    jq '.services["coaching-service"].environment.Coaching__Reminders__Enabled="true"' production.override.json > next.override.json
    mv next.override.json production.override.json
    "${compose[@]}" -f production.override.json up -d --no-deps --pull never coaching-service
    waitService coaching-service 8080 /health/ready
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    pattern='^eduivme-production-(identity-service|notification-service|coaching-service|admin-panel|staff-portal)-1 '
    diff -u <(grep -Ev "$pattern" containers-before.txt) <(grep -Ev "$pattern" containers-after.txt)
    date -u +%FT%TZ > deployed.txt
    trap - EXIT
    ;;
  validate) sha256sum -c payload.sha256 >/dev/null ;;
  *) echo 'Supported: prepare/build/backup-drill/verify-drill/migrate/verify-rollback/promote/validate' >&2; exit 2 ;;
esac

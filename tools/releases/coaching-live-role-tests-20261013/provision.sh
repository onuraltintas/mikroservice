#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-live-role-tests-20261013-r1
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
label=coaching-live-20261013-r1
unset FIXTURE_LIVE_AUTHORIZED
export FIXTURE_STATE=/fixture-state/credentials.json
db_user=$(docker exec postgres printenv POSTGRES_USER)
network=eduplatform-production
connection() {
  docker inspect "eduivme-production-$1-service-1" | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")'
}
unchanged() {
  for service in identity coaching; do
    [[ $(docker inspect "eduivme-production-$service-service-1" --format '{{.Image}}') == "$(<"$service-image.txt")" ]]
  done
}
runFixture() {
  local service=$1 action=$2 assembly
  if [[ "$service" == identity ]]; then assembly=IdentityFixture; else assembly=CoachingFixture; fi
  docker run --rm --user 0 --network "$network" --entrypoint dotnet \
    --env IDENTITY_CONNECTION --env COACHING_CONNECTION --env FIXTURE_STATE --env FIXTURE_LIVE_AUTHORIZED \
    -v "$release/runtime/$service:/runtime:ro" -v "$release/state:/fixture-state" \
    "$(<"$service-image.txt")" "/runtime/$assembly.dll" "$action"
}
export IDENTITY_CONNECTION COACHING_CONNECTION
IDENTITY_CONNECTION=$(connection identity)
COACHING_CONNECTION=$(connection coaching)
[[ "$IDENTITY_CONNECTION" == *'Database=identity_db;'* ]]
[[ "$COACHING_CONNECTION" == *'Database=coaching_db;'* ]]
case "${1:-verify}" in
  prepare)
    test ! -e prepared.txt; sha256sum -c payload.sha256 > payload-verified.log
    for service in identity coaching; do
      docker inspect "eduivme-production-$service-service-1" --format '{{.Image}}' > "$service-image.txt"
    done
    mkdir -m 700 state
    runFixture identity new-state
    chmod 600 state/credentials.json
    date -u +%FT%TZ > prepared.txt
    ;;
  drill|repeat-drill)
    test -s prepared.txt; test ! -e drill-complete.txt; unchanged
    for service in identity coaching; do
      database="${service}_db"; clone="coaching_roles_drill_20261013_$service"
      if [[ "$1" == drill ]]; then
        test ! -e "$database.before-roles.backup"
        docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-roles.backup"
      else
        test ! -e provisioned.txt; test -s "$database.before-roles.backup"
        [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$clone'") == 1 ]]
        docker exec postgres dropdb -U "$db_user" "$clone"
      fi
      docker exec -i postgres pg_restore --list < "$database.before-roles.backup" > "$database.backup-manifest.txt"
      [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$clone'") == 0 ]]
      docker exec postgres createdb -U "$db_user" "$clone"
      docker exec -i postgres pg_restore -U "$db_user" -d "$clone" --exit-on-error --no-owner --no-privileges < "$database.before-roles.backup" > "$service-restore.log" 2>&1
    done
    production_identity=$IDENTITY_CONNECTION
    IDENTITY_CONNECTION=${IDENTITY_CONNECTION/Database=identity_db;/Database=coaching_roles_drill_20261013_identity;}
    COACHING_CONNECTION=${COACHING_CONNECTION/Database=coaching_db;/Database=coaching_roles_drill_20261013_coaching;}
    runFixture identity identity > identity-drill.json
    runFixture coaching coaching > coaching-drill.json
    runFixture identity verify > identity-drill-verified.json
    runFixture coaching verify > coaching-drill-verified.json
    institution=$(jq -er '.InstitutionId' state/credentials.json)
    subscription=$(jq -er '.SubscriptionId' state/credentials.json)
    [[ "$institution" =~ ^[a-f0-9-]{36}$ && "$subscription" =~ ^[a-f0-9-]{36}$ ]]
    docker exec postgres psql -U "$db_user" -d coaching_roles_drill_20261013_identity -v ON_ERROR_STOP=1 -c "UPDATE identity.teacher_student_assignments SET \"IsActive\"=false WHERE \"InstitutionId\"='$institution'" > assignment-drift.log
    if runFixture identity verify > assignment-drift-rejected.log 2>&1; then echo 'Membership drift verification failed.' >&2; exit 2; fi
    grep -F 'Fixture active membership graph failed' assignment-drift-rejected.log >/dev/null
    docker exec postgres psql -U "$db_user" -d coaching_roles_drill_20261013_identity -v ON_ERROR_STOP=1 -c "UPDATE identity.teacher_student_assignments SET \"IsActive\"=true WHERE \"InstitutionId\"='$institution'" >> assignment-drift.log
    docker exec postgres psql -U "$db_user" -d coaching_roles_drill_20261013_coaching -v ON_ERROR_STOP=1 -c "UPDATE coaching.subscriptions SET \"Status\"='Cancelled' WHERE \"Id\"='$subscription'" > license-drift.log
    if runFixture coaching verify > license-drift-rejected.log 2>&1; then echo 'License drift verification failed.' >&2; exit 2; fi
    grep -F 'Fixture private-license isolation failed' license-drift-rejected.log >/dev/null
    docker exec postgres psql -U "$db_user" -d coaching_roles_drill_20261013_coaching -v ON_ERROR_STOP=1 -c "UPDATE coaching.subscriptions SET \"Status\"='Active' WHERE \"Id\"='$subscription'" >> license-drift.log
    runFixture identity verify > identity-drill-verified.json
    runFixture coaching verify > coaching-drill-verified.json
    if runFixture identity identity > duplicate-rejected.log 2>&1; then echo 'Duplicate fixture guard failed.' >&2; exit 2; fi
    grep -F 'never overwrite existing accounts' duplicate-rejected.log >/dev/null
    IDENTITY_CONNECTION=$production_identity
    if runFixture identity verify > live-authorization-rejected.log 2>&1; then echo 'Live authorization guard failed.' >&2; exit 2; fi
    grep -F 'Explicit live fixture authorization is required' live-authorization-rejected.log >/dev/null
    sha256sum *.before-roles.backup > backups.sha256
    date -u +%FT%TZ > drill-complete.txt
    ;;
  live)
    test -s drill-complete.txt; test ! -e provisioned.txt; unchanged
    sha256sum -c backups.sha256 > backups-verified.log
    export FIXTURE_LIVE_AUTHORIZED=$label
    runFixture identity identity > identity-live.json
    runFixture coaching coaching > coaching-live.json
    runFixture identity verify > fixture-public.json
    runFixture coaching verify > coaching-live-verified.json
    date -u +%FT%TZ > provisioned.txt
    cat fixture-public.json
    ;;
  verify)
    test -s provisioned.txt; unchanged
    export FIXTURE_LIVE_AUTHORIZED=$label
    runFixture identity verify
    runFixture coaching verify
    ;;
  *) echo 'Supported: prepare/drill/repeat-drill/live/verify' >&2; exit 2 ;;
esac

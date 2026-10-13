#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-test-account-update-20261013
original=/var/lib/eduivme/releases/coaching-live-role-tests-20261013-r1
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
unset FIXTURE_LIVE_AUTHORIZED
export FIXTURE_STATE=/update-state/credentials.json FIXTURE_UPDATE_REQUEST=/update-state/request.json
connection() { docker inspect "eduivme-production-$1-service-1" | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")'; }
export IDENTITY_CONNECTION COACHING_CONNECTION
IDENTITY_CONNECTION=$(connection identity); COACHING_CONNECTION=$(connection coaching)
db_user=$(docker exec postgres printenv POSTGRES_USER)
run() {
  local service=$1 action=$2 assembly
  if [[ "$service" == identity ]]; then assembly=IdentityAccountUpdate; else assembly=CoachingAccountUpdate; fi
  [[ $(docker inspect "eduivme-production-$service-service-1" --format '{{.Image}}') == "$(<"$original/$service-image.txt")" ]]
  docker run --rm --user 0 --network eduplatform-production --entrypoint dotnet \
    --env IDENTITY_CONNECTION --env COACHING_CONNECTION --env FIXTURE_STATE --env FIXTURE_UPDATE_REQUEST --env FIXTURE_LIVE_AUTHORIZED \
    -v "$release/runtime/$service:/runtime:ro" -v "$release/state:/update-state" \
    "$(<"$original/$service-image.txt")" "/runtime/$assembly.dll" "$action"
}
case "${1:-verify}" in
  drill)
    test ! -e drill-complete.txt; test ! -e applied.txt; test -s state/request.json
    cp "$original/state/credentials.json" state/credentials.json
    cp state/credentials.json state/credentials.before.json
    chmod 600 state/*.json
    for service in identity coaching; do
      database="${service}_db"; clone="coaching_account_update_20261013_$service"
      test ! -e "$database.before-update.backup"
      docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-update.backup"
      [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$clone'") == 0 ]]
      docker exec postgres createdb -U "$db_user" "$clone"
      docker exec -i postgres pg_restore -U "$db_user" -d "$clone" --exit-on-error --no-owner --no-privileges < "$database.before-update.backup" > "$service-restore.log" 2>&1
    done
    IDENTITY_CONNECTION=${IDENTITY_CONNECTION/Database=identity_db;/Database=coaching_account_update_20261013_identity;}
    COACHING_CONNECTION=${COACHING_CONNECTION/Database=coaching_db;/Database=coaching_account_update_20261013_coaching;}
    run identity preflight > identity-preflight.json; run coaching preflight > coaching-preflight.json
    run coaching apply > coaching-drill.json; run identity apply > identity-drill.json
    run identity verify > identity-drill-verified.json; run coaching verify > coaching-drill-verified.json
    # A second run must keep the original one-month target, rather than extend again.
    run coaching apply > coaching-repeat-drill.json; run coaching verify >> coaching-repeat-drill.json
    cp state/credentials.before.json state/credentials.json
    sha256sum *.before-update.backup > backups.sha256
    date -u +%FT%TZ > drill-complete.txt
    ;;
  live)
    test -s drill-complete.txt; test ! -e applied.txt
    sha256sum -c backups.sha256 > backups-verified.log
    export FIXTURE_LIVE_AUTHORIZED=coaching-account-update-20261013
    run identity preflight > identity-live-preflight.json; run coaching preflight > coaching-live-preflight.json
    run coaching apply > coaching-live.json; run identity apply > identity-live.json
    run identity verify > identity-live-verified.json; run coaching verify > coaching-live-verified.json
    cp state/credentials.json "$original/state/credentials.json.updated"
    chmod 600 "$original/state/credentials.json.updated"
    mv "$original/state/credentials.json.updated" "$original/state/credentials.json"
    date -u +%FT%TZ > applied.txt
    cat identity-live-verified.json coaching-live-verified.json
    ;;
  verify)
    test -s applied.txt; export FIXTURE_LIVE_AUTHORIZED=coaching-account-update-20261013
    run identity verify; run coaching verify
    ;;
  *) echo 'Supported: drill/live/verify' >&2; exit 2 ;;
esac

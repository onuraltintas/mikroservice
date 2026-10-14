#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/multi-institution-fixtures-20261014-r2
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 8
parent=/var/lib/eduivme/releases/multi-institution-enrollment-20261014
image=eduivme/identity-service:multi-institution-enrollment-20261014
database=multi_institution_fixture_drill_20261014
db_user=$(docker exec postgres printenv POSTGRES_USER)
sha256sum -c payload.sha256 > payload-verified.log
[[ $(docker inspect eduivme-production-identity-service-1 --format '{{.Image}}') == $(docker image inspect "$image" --format '{{.Id}}') ]]
docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
connection=$(jq -er '.ConnectionStrings__DefaultConnection' "$parent/identity-service.environment.json")
[[ "$connection" == *'Database=identity_db;'* ]]
runner() {
 docker run --rm --network eduplatform-production --entrypoint dotnet --env FIXTURE_CONNECTION --env FIXTURE_LIVE_AUTHORIZED -v "$release/payload/fixture:/fixture:ro" "$image" /fixture/Fixture.dll "$1"
}
snapshot() {
 docker exec -i postgres psql -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" -At < "$parent/source-preservation.sql"
}
case "${1:-verify}" in
 drill)
  test ! -e drill-verified.txt
  [[ $(docker exec postgres psql -v ON_ERROR_STOP=1 -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
  (cd "$parent" && sha256sum -c backup.sha256) > backup-verified.log
  docker exec postgres createdb -U "$db_user" "$database"
  trap 'docker exec postgres dropdb -U "$db_user" "$database" >/dev/null 2>&1 || true' EXIT
  docker exec -i postgres pg_restore -U "$db_user" -d "$database" --exit-on-error --no-owner --no-privileges < "$parent/identity.before-enrollment.backup" > clone-restore.log 2>&1
  export FIXTURE_CONNECTION=${connection/Database=identity_db;/Database=$database;}
  export FIXTURE_LIVE_AUTHORIZED=''
  snapshot "$database" > source-before.txt
  runner grant > clone-grant.json
  jq -e '.added == 3 and .database == "multi_institution_fixture_drill_20261014"' clone-grant.json >/dev/null
  runner verify > clone-verify.json
  runner grant > clone-repeat.json
  jq -e '.added == 0' clone-repeat.json >/dev/null
  snapshot "$database" > source-after.txt
  diff -u source-before.txt source-after.txt
  docker exec -i postgres psql -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" > clone-revocation.log <<'SQL'
UPDATE identity."UserProductAccesses" SET "IsActive"=false WHERE "UserId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND "Product"='SpeedReading';
SQL
  if runner grant > clone-revoked-result.log 2>&1; then echo 'Revoked test access was incorrectly reactivated' >&2; exit 1; fi
  grep -qF 'Existing target fixture access must not be changed or reactivated' clone-revoked-result.log
  date -u +%FT%TZ > drill-verified.txt
  cat clone-grant.json clone-repeat.json
  ;;
 grant)
  test -s drill-verified.txt; test ! -e granted.txt
  export FIXTURE_CONNECTION="$connection"
  export FIXTURE_LIVE_AUTHORIZED=multi-institution-fixtures-20261014
  docker exec postgres pg_dump -U "$db_user" -d identity_db -Fc > identity-before-fixture.backup
  sha256sum identity-before-fixture.backup > backup.sha256
  snapshot identity_db > live-source-before.txt
  runner grant > live-grant.json
  runner verify > live-verify.json
  snapshot identity_db > live-source-after.txt
  diff -u live-source-before.txt live-source-after.txt
  date -u +%FT%TZ > granted.txt
  cat live-grant.json
  ;;
 verify)
  test -s granted.txt
  export FIXTURE_CONNECTION="$connection"
  export FIXTURE_LIVE_AUTHORIZED=multi-institution-fixtures-20261014
  runner verify
  ;;
 *) exit 2 ;;
esac

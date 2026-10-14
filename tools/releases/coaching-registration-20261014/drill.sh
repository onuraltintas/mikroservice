#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-registration-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
database=coaching_registration_drill_20261014
image=sha256:da3159ce4d40d86dbf392d2ebee409b47e842fd4442e6a8ba6a0dc7e7e2de2d7
[[ $(docker inspect eduivme-production-identity-service-1 --format '{{.Image}}') == "$image" ]]
db_user=$(docker exec postgres printenv POSTGRES_USER)
if test ! -s identity.before-registration.backup; then
  docker exec postgres pg_dump -U "$db_user" -d identity_db -Fc > identity.before-registration.backup
  sha256sum identity.before-registration.backup > backup.sha256
fi
sha256sum -c backup.sha256 > backup-verified.log
if [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]; then
  docker exec postgres createdb -U "$db_user" "$database"
  docker exec -i postgres pg_restore -U "$db_user" -d "$database" --exit-on-error --no-owner --no-privileges < identity.before-registration.backup > restore.log 2>&1
fi
connection=$(docker inspect eduivme-production-identity-service-1 | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")')
[[ "$connection" == *'Database=identity_db;'* ]]
export REGISTRATION_CONNECTION=${connection/Database=identity_db;/Database=$database;}
docker run --rm --network eduplatform-production --entrypoint dotnet --env REGISTRATION_CONNECTION \
  -v "$release/payload/verification:/verification:ro" "$image" /verification/Verification.dll > relational-results.json
jq -e '.passed == 7 and (.checks | index("rollback")) != null' relational-results.json >/dev/null
docker exec postgres dropdb -U "$db_user" "$database"
date -u +%FT%TZ > drill-verified.txt
cat relational-results.json

set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-lgs-other-20261018
drill=coaching_lgs_other_drill_20261018
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
db_user=$(docker exec postgres printenv POSTGRES_USER)
image=$(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}')
network=$(docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].NetworkSettings.Networks|keys[0]')
rows() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L||%L||md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,'|',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState') ORDER BY table_name
\gexec
SQL
}
accounts() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
}
preserved() {
 rows "$1" > "$2.after.txt"
 LC_ALL=C comm -23 "$2.before.txt" "$2.after.txt" > "$2.missing-or-modified.txt"
 test ! -s "$2.missing-or-modified.txt"
}
run() {
 local mode=$1
 export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID VERIFY_APPROVAL
 VERIFY_SCOPE=$mode; VERIFY_APPROVAL=user-approved-lgs-only-20261018
 VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json)
 if [[ "$mode" == drill ]]; then
   VERIFY_CONNECTION=$(jq -nr --arg c "$VERIFY_CONNECTION" --arg d "$drill" '$c|sub("Database=coaching_db;";"Database="+$d+";")')
   [[ "$VERIFY_CONNECTION" == *"Database=$drill;"* ]]
 fi
 VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
 [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
 docker run --rm --network "$network" --entrypoint sh -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -e VERIFY_APPROVAL \
   -v "$release/payload:/verification:ro" "$image" \
   -c 'mkdir /tmp/lgs-verification && cp /verification/Verification.* /tmp/lgs-verification/ && cp /app/*.dll /tmp/lgs-verification/ && dotnet /tmp/lgs-verification/Verification.dll'
}
sha256sum -c payload.sha256 > payload-verified.log
case "$1" in
 prepare)
  test ! -e prepared.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}' > service.before.txt
  docker inspect eduivme-production-coaching-service-1 | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > coaching.environment.json
  docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.Domain.dll /app/Coaching.Infrastructure.dll | sed 's#/app/##' > live-assemblies.sha256
  cmp expected-assemblies.sha256 live-assemblies.sha256
  for database in identity_db coaching_db; do
   docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-import.backup"
  done
  sha256sum *.before-import.backup > backups.sha256
  accounts > accounts.before.txt
  date -u +%FT%TZ > prepared.txt
  echo 'Backups and exact live assembly verification ready.'
  ;;
 drill)
  test -s prepared.txt; test ! -e drill-verified.txt
  if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]; then
   connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json)
   owner_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'
   [[ "$connection" =~ $owner_regex ]]; owner=${BASH_REMATCH[2]}
   docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
   docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before-import.backup > drill-restore.log 2>&1
   rows "$drill" > drill.before.txt
  else
   # A failed transaction may be retried only on this release's untouched clone.
   test -s drill.before.txt
   rows "$drill" > drill.retry.txt
   cmp drill.before.txt drill.retry.txt
  fi
  run drill > drill-result.json 2> drill-error.log
  jq -e '.passed and .insertedOutcomes==217 and .totalNewCatalogOutcomes==217' drill-result.json >/dev/null
  preserved "$drill" drill
  rows "$drill" > replay.before.txt
  run drill > drill-replay-result.json 2> drill-replay-error.log
  jq -e '.passed and .insertedOutcomes==0' drill-replay-result.json >/dev/null
  rows "$drill" > replay.after.txt
  cmp replay.before.txt replay.after.txt
  date -u +%FT%TZ > drill-verified.txt
  cat drill-result.json
  ;;
 live)
  test -s drill-verified.txt; test ! -e live-verified.txt
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}') == "$(<service.before.txt)" ]]
  # A fresh pre-import backup also captures work saved during the rehearsal.
  docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.immediate-before-import.backup
  sha256sum coaching_db.immediate-before-import.backup > immediate-backup.sha256
  rows coaching_db > live.before.txt
  accounts > accounts.immediate-before.txt
  run live > live-result.json 2> live-error.log
  jq -e '.passed and .insertedOutcomes==217' live-result.json >/dev/null
  preserved coaching_db live
  accounts > accounts.after.txt
  cmp accounts.immediate-before.txt accounts.after.txt
  rows coaching_db > live-replay.before.txt
  run live > live-replay-result.json 2> live-replay-error.log
  jq -e '.passed and .insertedOutcomes==0' live-replay-result.json >/dev/null
  rows coaching_db > live-replay.after.txt
  cmp live-replay.before.txt live-replay.after.txt
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}') == "$(<service.before.txt)" ]]
  date -u +%FT%TZ > live-verified.txt
  cat live-result.json
  ;;
 verify)
  test -s live-verified.txt
  preserved coaching_db live
  accounts > accounts.latest.txt; cmp accounts.immediate-before.txt accounts.latest.txt
  cat live-verified.txt; cat live-result.json; cat live-replay-result.json
  ;;
 *) exit 2;;
esac

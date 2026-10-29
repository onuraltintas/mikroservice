#!/bin/sh
set -eu
umask 077
release=${RELEASE_DIR:?Set RELEASE_DIR to the private release directory prepared by prepare-release.mjs}
database=${CHECK_DATABASE:?Set CHECK_DATABASE to a new disposable database name}
service=eduivme-production-speed-reading-service-1
test -z "$(docker exec postgres psql -X -qAt -U eduplatform -d postgres -c "SELECT datname FROM pg_database WHERE datname='$database'")"
docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > "$release/speedreading-before.dump"
docker exec -i postgres pg_restore --list < "$release/speedreading-before.dump" > "$release/backup-contents.txt"
sha256sum "$release/speedreading-before.dump" > "$release/backup.sha256"
docker exec postgres createdb -U eduplatform "$database"
cleanup() {
 docker exec postgres dropdb -U eduplatform --if-exists "$database"
 rm -f "$release/audit.env"
}
trap cleanup EXIT
docker exec -i postgres pg_restore -U eduplatform -d "$database" --exit-on-error < "$release/speedreading-before.dump"
query() { docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$database" < "$release/$1"; }
query inventory-all.sql > "$release/rehearsal-before.txt"
query inventory-protected.sql > "$release/rehearsal-protected-before.txt"
query apply.sql > "$release/rehearsal-apply.log"
query verify.sql
query snapshot.sql > "$release/rehearsal-after.json"
query inventory-protected.sql > "$release/rehearsal-protected-after.txt"
cmp "$release/rehearsal-protected-before.txt" "$release/rehearsal-protected-after.txt"
query inventory-all.sql > "$release/rehearsal-after-first.txt"
query apply.sql > "$release/rehearsal-idempotence.log"
query inventory-all.sql > "$release/rehearsal-after-second.txt"
cmp "$release/rehearsal-after-first.txt" "$release/rehearsal-after-second.txt"
docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$service" | sed "s/Database=speedreading_owned_db/Database=$database/g" > "$release/audit.env"
image=$(docker inspect --format '{{.Config.Image}}' "$service")
docker run --rm --network eduplatform-production --env-file "$release/audit.env" "$image" --audit-training-programs > "$release/rehearsal-audit.log" 2>&1
grep -E '20|560|3640|failed|Failed' "$release/rehearsal-audit.log" | tail -n 5
query rollback.sql > "$release/rehearsal-rollback.log"
query inventory-all.sql > "$release/rehearsal-after-rollback.txt"
cmp "$release/rehearsal-before.txt" "$release/rehearsal-after-rollback.txt"
printf '%s\n' 'PASS: protected data unchanged, idempotence, runtime schedule audit, exact rollback'

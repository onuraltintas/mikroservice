#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/comprehension-20261011-v1
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another Speed Reading release is running' >&2; exit 2; }
pack="$release/content-packs/comprehension/v1"
backend=eduivme-production-speed-reading-service-1
validation_db=eduivme_comprehension_check_20261011
psql_owned() { docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U eduplatform -d "$1" "${@:2}"; }
backup() {
  docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > "$1"
  test -s "$1"
  docker exec -i postgres pg_restore --list < "$1" > "$1.contents.txt"
  sha256sum "$1" > "$1.sha256"
}
case "${1:-rehearse}" in
  rehearse)
    test ! -e rehearsal-before.dump
    sha256sum -c payload.sha256
    backup rehearsal-before.dump
    docker exec postgres createdb -U eduplatform -T template0 "$validation_db"
    trap 'docker exec postgres dropdb -U eduplatform "$validation_db"' EXIT
    docker exec -i postgres pg_restore -U eduplatform --no-owner --no-privileges -d "$validation_db" < rehearsal-before.dump
    psql_owned "$validation_db" -qAt < "$pack/inventory.sql" > rehearsal-before.txt
    psql_owned "$validation_db" < "$pack/apply.sql"
    psql_owned "$validation_db" < "$pack/apply.sql"
    psql_owned "$validation_db" -qAt < "$pack/inventory.sql" > rehearsal-after.txt
    diff -u rehearsal-before.txt rehearsal-after.txt
    psql_owned "$validation_db" < "$pack/rollback.sql"
    psql_owned "$validation_db" < "$pack/rollback.sql"
    psql_owned "$validation_db" -qAt < "$pack/inventory.sql" > rehearsal-rollback.txt
    diff -u rehearsal-before.txt rehearsal-rollback.txt
    date -u +%FT%TZ > rehearsal-passed.txt
    ;;
  promote)
    test -s rehearsal-passed.txt
    test ! -e speedreading-before.dump
    sha256sum -c payload.sha256
    docker inspect "$backend" --format '{{.Id}} {{.Config.Image}}' > api-before.txt
    docker ps --format '{{.ID}} {{.Names}} {{.Image}}' > containers-before.txt
    docker stop --time 30 "$backend"
    trap 'docker start "$backend" >/dev/null' EXIT
    backup speedreading-before.dump
    psql_owned speedreading_owned_db -qAt < "$pack/inventory.sql" > inventory-before.txt
    # If SQL has committed but integrity checks fail, leave the API stopped for inspection.
    trap 'echo "Inspect SQL state and scoped rollback; API left stopped" >&2' EXIT
    psql_owned speedreading_owned_db < "$pack/apply.sql"
    psql_owned speedreading_owned_db -qAt < "$pack/inventory.sql" > inventory-after.txt
    diff -u inventory-before.txt inventory-after.txt
    docker start "$backend"
    trap - EXIT
    docker inspect "$backend" --format '{{.Id}} {{.Config.Image}}' > api-after.txt
    diff -u api-before.txt api-after.txt
    docker ps --format '{{.ID}} {{.Names}} {{.Image}}' > containers-after.txt
    diff -u <(sort containers-before.txt) <(sort containers-after.txt)
    date -u +%FT%TZ > deployed-at.txt
    ;;
  *) exit 2 ;;
esac

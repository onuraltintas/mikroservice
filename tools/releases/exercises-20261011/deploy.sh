#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/exercises-20261011-6f315ff3
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another Speed Reading release is running' >&2; exit 2; }
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
admin=eduivme-production-admin-panel-1
validation_db=eduivme_release_check_6f315ff3
psql_owned() { docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U eduplatform -d "$1" "${@:2}"; }
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
declare -A seen=()
export TEEN_API_IMAGE=eduivme/speed-reading-service:teen-programs-v2-534c187a
for container in "$backend" "$frontend" "$admin"; do
  files=$(docker inspect "$container" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
  IFS=',' read -r -a config_files <<< "$files"
  for file in "${config_files[@]}"; do
    test -f "$file"
    if [[ -z "${seen[$file]:-}" ]]; then compose+=(-f "$file"); seen[$file]=1; fi
  done
done
compose+=(-f "$release/production.override.yml")
packs=(tachistoscope-settings/v2 guided-reading/v1 scanning/v1 grouping/v1 text-fading/v1
  visual-expansion/v1 fixation/v1 saccade/v1 regression/v2 subvocalization/v2 visualization/v2)
make_batch() {
  printf 'BEGIN;\n'
  for pack in "${packs[@]}"; do
    file="$release/source/content-packs/$pack/apply.sql"
    [[ $(grep -Ec '^BEGIN;[[:space:]]*$' "$file") == 1 ]]
    [[ $(grep -Ec '^COMMIT;[[:space:]]*$' "$file") == 1 ]]
    printf '\n-- Package: %s\n' "$pack"
    sed -E '/^BEGIN;[[:space:]]*$/d; /^COMMIT;[[:space:]]*$/d' "$file"
  done
  printf '\nCOMMIT;\n'
}
case "${1:-validate}" in
  validate)
    "${compose[@]}" config --quiet
    make_batch > apply-all.sql
    sha256sum apply-all.sql > apply-all.sha256
    ;;
  build)
    docker build -f api.Dockerfile -t eduivme/speed-reading-service:exercises-20261011-6f315ff3 .
    docker build -f web.Dockerfile -t eduivme/speed-reading-frontend:exercises-20261011-6f315ff3 .
    docker build -f admin.Dockerfile -t eduivme/admin-panel:exercises-20261011-6f315ff3 .
    ;;
  rehearse)
    test ! -e rehearsal-before.dump
    docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > rehearsal-before.dump
    docker exec -i postgres pg_restore --list < rehearsal-before.dump > rehearsal-backup-contents.txt
    docker exec postgres createdb -U eduplatform -T template0 "$validation_db"
    trap 'docker exec postgres dropdb -U eduplatform "$validation_db"' EXIT
    docker exec -i postgres pg_restore -U eduplatform --no-owner --no-privileges -d "$validation_db" < rehearsal-before.dump
    psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-inventory-before.txt
    sha256sum -c apply-all.sha256
    psql_owned "$validation_db" < apply-all.sql
    psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-inventory-after.txt
    diff -u rehearsal-inventory-before.txt rehearsal-inventory-after.txt
    date -u +%FT%TZ > rehearsal-passed.txt
    ;;
  promote)
    test -s rehearsal-passed.txt
    test ! -e speedreading-before.dump
    sha256sum -c apply-all.sha256
    "${compose[@]}" config --quiet
    docker ps --format '{{.ID}} {{.Names}} {{.Image}}' > containers-before.txt
    for container in "$backend" "$frontend" "$admin"; do
      docker inspect "$container" --format '{{.Name}} {{.Config.Image}}' >> previous-images.txt
    done
    docker stop --time 30 "$backend"
    # Until SQL starts, any failed backup/check is safe to recover by restarting the old API.
    trap 'docker start "$backend" >/dev/null' EXIT
    docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > speedreading-before.dump
    docker exec -i postgres pg_restore --list < speedreading-before.dump > backup-contents.txt
    test -s speedreading-before.dump
    sha256sum speedreading-before.dump > backup.sha256
    psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-before.txt
    trap 'echo "SQL/application state needs inspection; API left stopped on failure" >&2' EXIT
    psql_owned speedreading_owned_db < apply-all.sql
    psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-after.txt
    diff -u inventory-before.txt inventory-after.txt
    "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend admin-panel
    trap - EXIT
    date -u +%FT%TZ > deployed-at.txt
    docker ps --format '{{.ID}} {{.Names}} {{.Image}}' > containers-after.txt
    ;;
  *) exit 2 ;;
esac

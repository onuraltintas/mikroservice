#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/curriculum-v3-20261012
[[ "$PWD" == "$release" ]] || { echo 'Wrong release directory' >&2; exit 2; }
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another release is running' >&2; exit 2; }
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
validation_db=eduivme_curriculum_v3_check_20261012
api_image=eduivme/speed-reading-service:curriculum-v3-20261012
web_image=eduivme/speed-reading-frontend:curriculum-v3-20261012
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
psql_owned() { docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U eduplatform -d "$1" "${@:2}"; }
backup() {
  test ! -e "$1"
  docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > "$1"
  test -s "$1"
  docker exec -i postgres pg_restore --list < "$1" > "$1.contents.txt"
  sha256sum "$1" > "$1.sha256"
}
prepare_compose() {
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  declare -A seen=()
  export TEEN_API_IMAGE
  TEEN_API_IMAGE=$(docker inspect "$backend" --format '{{.Config.Image}}')
  for container in "$backend" "$frontend"; do
    files=$(docker inspect "$container" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
    IFS=',' read -r -a config_files <<< "$files"
    for file in "${config_files[@]}"; do
      test -f "$file"
      if [[ -z "${seen[$file]:-}" ]]; then compose+=(-f "$file"); seen[$file]=1; fi
    done
  done
  compose+=(-f "$release/production.override.yml")
  "${compose[@]}" config --quiet
}
prepare_environment() {
  docker inspect "$backend" --format '{{range .Config.Env}}{{println .}}{{end}}' > runtime.env
  network=$(docker inspect "$backend" --format '{{range $name, $value := .NetworkSettings.Networks}}{{println $name}}{{end}}' | head -n1)
  test -n "$network"
  grep -q 'Database=speedreading_owned_db' runtime.env
  sed "s/Database=speedreading_owned_db/Database=$validation_db/g" runtime.env > rehearsal.env
}
apply_pack() {
  psql_owned "$1" -v "repair_progress_id=$(<repair-progress-id.txt)" -v "repair_schedule_md5=$(<repair-schedule-md5.txt)" < apply-all.sql
}
run_cli() { docker run --rm --network "$network" --env-file "$1" "$api_image" "$2"; }
check_images() {
  for image in "$api_image" "$web_image"; do
    [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
  done
}
case "${1:-validate}" in
  validate) sha256sum -c payload.sha256; prepare_compose ;;
  build)
    test ! -e previous-api-image.txt
    sha256sum -c payload.sha256
    prepare_compose
    docker inspect "$backend" --format '{{.Image}}' > previous-api-image.txt
    docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
    docker inspect "$backend" --format '{{.Config.Image}}' > previous-api-tag.txt
    docker inspect "$frontend" --format '{{.Config.Image}}' > previous-web-tag.txt
    mkdir -p previous/browser
    psql_owned speedreading_owned_db -qAt -c "SELECT id FROM speed_reading.student_program_progress WHERE \"ProgramTemplateId\"='e2c01002-8d9a-4e6c-a428-000000000001'::uuid" > repair-progress-id.txt
    psql_owned speedreading_owned_db -qAt -c "SELECT md5(\"ScheduleJson\"::text) FROM speed_reading.student_program_progress WHERE \"ProgramTemplateId\"='e2c01002-8d9a-4e6c-a428-000000000001'::uuid" > repair-schedule-md5.txt
    [[ $(<repair-progress-id.txt) =~ ^[a-f0-9-]{36}$ && $(<repair-schedule-md5.txt) =~ ^[a-f0-9]{32}$ ]]
    docker cp "$frontend:/usr/share/nginx/html/." previous/browser/
    docker build --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "$api_image" .
    docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$web_image" .
    check_images
    ;;
  rehearse)
    sha256sum -c payload.sha256
    check_images; prepare_environment
    if test -s rehearsal-before.dump; then sha256sum -c rehearsal-before.dump.sha256; else backup rehearsal-before.dump; fi
    docker exec postgres createdb -U eduplatform -T template0 "$validation_db"
    trap 'docker exec postgres dropdb -U eduplatform "$validation_db"' EXIT
    # Same cluster: all original roles exist. Preserve owners/ACLs so the
    # application account, not only the backup administrator, can migrate.
    docker exec -i postgres pg_restore -U eduplatform -d "$validation_db" < rehearsal-before.dump
    psql_owned "$validation_db" -qAt < inventory-protected.sql > rehearsal-before.txt
    run_cli rehearsal.env --migrate-only > rehearsal-migration.log 2>&1
    apply_pack "$validation_db"
    psql_owned "$validation_db" < verify.sql
    run_cli rehearsal.env --audit-training-programs > rehearsal-runtime-audit.log 2>&1
    grep -q '"Programs":20,"Days":560,"Slots":3640' rehearsal-runtime-audit.log
    psql_owned "$validation_db" -qAt < inventory-protected.sql > rehearsal-after.txt
    diff -u rehearsal-before.txt rehearsal-after.txt
    psql_owned "$validation_db" -qAt < inventory-all.sql > rehearsal-first.txt
    apply_pack "$validation_db"
    psql_owned "$validation_db" -qAt < inventory-all.sql > rehearsal-second.txt
    diff -u rehearsal-first.txt rehearsal-second.txt
    psql_owned "$validation_db" < rollback.sql
    psql_owned "$validation_db" -qAt < inventory-protected.sql > rehearsal-rollback.txt
    diff -u rehearsal-before.txt rehearsal-rollback.txt
    date -u +%FT%TZ > rehearsal-passed.txt
    ;;
  promote)
    test -s rehearsal-passed.txt
    test ! -e deployed-at.txt
    sha256sum -c payload.sha256; check_images; prepare_compose; prepare_environment
    [[ $(docker inspect "$backend" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
    [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    docker stop --time 30 "$backend"
    trap 'docker start "$backend" >/dev/null' EXIT
    backup speedreading-before.dump
    psql_owned speedreading_owned_db -qAt < inventory-protected.sql > inventory-before.txt
    trap 'echo "Inspect backup/migration/data; API left stopped" >&2' EXIT
    run_cli runtime.env --migrate-only > production-migration.log 2>&1
    apply_pack speedreading_owned_db
    psql_owned speedreading_owned_db < verify.sql
    run_cli runtime.env --audit-training-programs > production-runtime-audit.log 2>&1
    grep -q '"Programs":20,"Days":560,"Slots":3640' production-runtime-audit.log
    psql_owned speedreading_owned_db -qAt < inventory-protected.sql > inventory-after.txt
    diff -u inventory-before.txt inventory-after.txt
    "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
    trap - EXIT
    date -u +%FT%TZ > deployed-at.txt
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    diff -u <(grep -Ev "^($backend|$frontend) " containers-before.txt) <(grep -Ev "^($backend|$frontend) " containers-after.txt)
    ;;
  *) exit 2 ;;
esac

#!/usr/bin/env bash
set -euo pipefail
umask 077
release=$PWD
[[ "$release" == /var/lib/eduivme/releases/continuation-* ]] || exit 2
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]] || exit 2
tag="continuation-20261012-${revision:0:8}"
api_image="eduivme/speed-reading-service:$tag"
web_image="eduivme/speed-reading-frontend:$tag"
staff_image="eduivme/staff-portal:$tag"
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
staff=eduivme-production-staff-portal-1
validation_db=eduivme_continuation_check_20261012
identity_check=eduivme_continuation_identity_check_20261012
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another release is running' >&2; exit 2; }
psql_owned() { docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U eduplatform -d "$1" "${@:2}"; }
backup() {
 test ! -e "$2"
 docker exec postgres pg_dump -U eduplatform -d "$1" -Fc > "$2"
 test -s "$2"
 docker exec -i postgres pg_restore --list < "$2" > "$2.contents.txt"
 sha256sum "$2" > "$2.sha256"
}
prepare_compose() {
 compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
 declare -A seen=()
 export TEEN_API_IMAGE
 TEEN_API_IMAGE=$(docker inspect "$backend" --format '{{.Config.Image}}')
 for container in "$backend" "$frontend" "$staff"; do
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
run_cli() { docker run --rm --network "$network" --env-file "$1" "$api_image" "$2"; }
check_images() {
 for image in "$api_image" "$web_image" "$staff_image"; do
  [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
 done
}
identity_inventory() {
 psql_owned "$1" -qAt -c "SELECT 'roles|'||count(*)||'|'||md5(coalesce(string_agg(md5(to_jsonb(r)::text),'' ORDER BY md5(to_jsonb(r)::text)),'')) FROM identity.\"Roles\" r;
 SELECT 'permissions|'||count(*)||'|'||md5(coalesce(string_agg(md5(to_jsonb(r)::text),'' ORDER BY md5(to_jsonb(r)::text)),'')) FROM identity.\"Permissions\" r WHERE \"Key\"<>'Permissions.SpeedReading.StudentProgramApprove';
 SELECT 'role-permissions|'||count(*)||'|'||md5(coalesce(string_agg(md5(to_jsonb(r)::text),'' ORDER BY md5(to_jsonb(r)::text)),'')) FROM identity.\"RolePermissions\" r WHERE \"Permission\"<>'Permissions.SpeedReading.StudentProgramApprove';"
}
verify() {
 [[ $(psql_owned "$1" -qAt -c "SELECT count(*) FROM speed_reading.__ef_migrations_history WHERE \"MigrationId\"='20261012131123_AddProgramContinuationDecisions'") == 1 ]]
 [[ $(psql_owned "$1" -qAt -c 'SELECT count(*) FROM speed_reading.program_continuations') == 0 ]]
}
ready() {
 for attempt in {1..20}; do
  ip=$(docker inspect "$backend" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -n1)
  if curl -fsS --max-time 4 "http://$ip:8080/health/ready" > health-api.json &&
   [[ $(docker inspect "$frontend" --format '{{.State.Health.Status}}') == healthy ]] &&
   [[ $(docker inspect "$staff" --format '{{.State.Health.Status}}') == healthy ]]; then return 0; fi
  sleep 3
 done
 return 1
}
case "${1:-build}" in
 build)
  test ! -e previous-api-image.txt
  sha256sum -c payload.sha256
  docker inspect "$backend" --format '{{.Image}}' > previous-api-image.txt
  docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
  docker inspect "$staff" --format '{{.Image}}' > previous-staff-image.txt
  mkdir -p previous/browser previous-staff/browser
  docker cp "$frontend:/usr/share/nginx/html/." previous/browser/
  docker cp "$frontend:/etc/nginx/conf.d/default.conf" nginx.conf
  docker cp "$staff:/usr/share/nginx/html/." previous-staff/browser/
  docker cp "$staff:/etc/nginx/conf.d/default.conf" staff-nginx.conf
  printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n  staff-portal:\n    image: %s\n    pull_policy: never\n' "$api_image" "$web_image" "$staff_image" > production.override.yml
  printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n  staff-portal:\n    image: %s\n    pull_policy: never\n' "$(<previous-api-image.txt)" "$(<previous-web-image.txt)" "$(<previous-staff-image.txt)" > rollback.override.yml
  prepare_compose
  docker build --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "$api_image" .
  docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$web_image" .
  docker build --build-arg "SOURCE_REVISION=$revision" -f staff.Dockerfile -t "$staff_image" .
  check_images
  docker run --rm "$web_image" nginx -t
  docker run --rm "$staff_image" nginx -t
  ;;
 rehearse)
  check_images; prepare_environment
  backup speedreading_owned_db rehearsal-before.dump
  backup identity_db rehearsal-identity.dump
  docker exec postgres createdb -U eduplatform -T template0 "$validation_db"
  trap 'docker exec postgres dropdb -U eduplatform "$validation_db"' EXIT
  docker exec -i postgres pg_restore -U eduplatform -d "$validation_db" < rehearsal-before.dump
  psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-before.txt
  run_cli rehearsal.env --migrate-only > rehearsal-migration.log 2>&1
  verify "$validation_db"
  run_cli rehearsal.env --audit-training-programs > rehearsal-runtime-audit.log 2>&1
  grep -q '"Programs":20,"Days":560,"Slots":3640' rehearsal-runtime-audit.log
  psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-after.txt
  diff -u rehearsal-before.txt rehearsal-after.txt
  docker exec postgres createdb -U eduplatform -T template0 "$identity_check"
  trap 'docker exec postgres dropdb -U eduplatform "$validation_db"; docker exec postgres dropdb -U eduplatform "$identity_check"' EXIT
  docker exec -i postgres pg_restore -U eduplatform -d "$identity_check" < rehearsal-identity.dump
  identity_inventory "$identity_check" > rehearsal-identity-before.txt
  psql_owned "$identity_check" < permission.sql
  psql_owned "$identity_check" < permission.sql
  identity_inventory "$identity_check" > rehearsal-identity-after.txt
  diff -u rehearsal-identity-before.txt rehearsal-identity-after.txt
  date -u +%FT%TZ > rehearsal-passed.txt
  ;;
 promote)
  test -s rehearsal-passed.txt
  test ! -e deployed-at.txt
  check_images; prepare_compose; prepare_environment
  [[ $(docker inspect "$backend" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
  [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
  [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<previous-staff-image.txt)" ]]
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
  docker stop --time 30 "$backend"
  rollback() { echo 'Restoring previous application images; additive table is retained' >&2; "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps --pull never speed-reading-service speed-reading-frontend staff-portal; }
  trap rollback ERR
  backup speedreading_owned_db speedreading-before.dump
  backup identity_db identity-before.dump
  psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-before.txt
  run_cli runtime.env --migrate-only > production-migration.log 2>&1
  verify speedreading_owned_db
  psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-after.txt
  diff -u inventory-before.txt inventory-after.txt
  identity_inventory identity_db > identity-before.txt
  psql_owned identity_db < permission.sql
  identity_inventory identity_db > identity-after.txt
  diff -u identity-before.txt identity-after.txt
  "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend staff-portal
  ready
  trap - ERR
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
  diff -u <(grep -Ev "^($backend|$frontend|$staff) " containers-before.txt) <(grep -Ev "^($backend|$frontend|$staff) " containers-after.txt)
  date -u +%FT%TZ > deployed-at.txt
  ;;
 *) exit 2 ;;
esac

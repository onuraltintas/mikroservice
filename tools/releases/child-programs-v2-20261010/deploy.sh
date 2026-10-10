#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/child-programs-v2-20261010-77618cb9
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
image_api=eduivme/speed-reading-service:child-programs-v2-77618cb9
image_web=eduivme/speed-reading-frontend:child-programs-v2-77618cb9
cd "$release"
psql_owned() { docker exec -i postgres psql -U eduplatform -d speedreading_owned_db -v ON_ERROR_STOP=1 "$@"; }
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
declare -A seen=()
for container in "$backend" "$frontend"; do
  files=$(docker inspect "$container" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')
  IFS=',' read -r -a config_files <<< "$files"
  for file in "${config_files[@]}"; do
    test -f "$file"
    if [[ -z "${seen[$file]:-}" ]]; then compose+=(-f "$file"); seen[$file]=1; fi
  done
done
compose+=(-f "$release/production.override.yml")
backend_stopped=false
recover() {
  code=$?
  trap - EXIT INT TERM
  if $backend_stopped && (( code != 0 )); then
    # Check the committed state, including a signal arriving just after SQL COMMIT.
    published=$(psql_owned -Atc "SELECT count(*) FROM speed_reading.program_templates WHERE created_by='system:child-programs-v2';") || published=unknown
    if [[ "$published" == 0 ]]; then
      docker start "$backend" >/dev/null || echo 'CRITICAL: previous backend restart failed' >&2
    elif [[ "$published" == 5 ]] && psql_owned -qAt < "$release/verify.sql" >/dev/null; then
      "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend \
        || echo 'CRITICAL: forward application recovery failed; keep schema and restore service manually' >&2
    else
      echo 'CRITICAL: publication state is not verified; manual service recovery required' >&2
    fi
  fi
  exit "$code"
}
case "${1:-validate}" in
  validate)
    "${compose[@]}" config --quiet
    psql_owned -At < preflight.sql
    ;;
  build)
    docker build -f api.Dockerfile -t "$image_api" .
    docker build -f web.Dockerfile -t "$image_web" .
    ;;
  backup)
    test ! -e speedreading-before.dump
    docker inspect "$backend" --format '{{.Config.Image}}' > previous-api-image.txt
    docker inspect "$frontend" --format '{{.Config.Image}}' > previous-web-image.txt
    docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > speedreading-before.dump
    test -s speedreading-before.dump
    docker exec -i postgres pg_restore --list < speedreading-before.dump > backup-contents.txt
    sha256sum speedreading-before.dump > backup.sha256
    psql_owned -At < inventory.sql > inventory-before.txt
    printf 'Backup validated: %s bytes\n' "$(stat -c %s speedreading-before.dump)"
    ;;
  promote)
    test -s speedreading-before.dump
    sha256sum -c backup.sha256
    "${compose[@]}" config --quiet
    psql_owned -At < preflight.sql
    trap recover EXIT
    trap 'exit 130' INT
    trap 'exit 143' TERM
    backend_stopped=true
    docker stop --time 30 "$backend"
    # Execute only the SQL generated for the single approved slot migration.
    psql_owned < task-slot-migration.sql
    plan_json=$(< child-program-plan-v2.json)
    docker exec -i postgres psql -U eduplatform -d speedreading_owned_db -v ON_ERROR_STOP=1 -v "plan_json=$plan_json" < apply.sql
    # Repeated-task content is now published: retain the forward schema and application.
    "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
    backend_stopped=false
    trap - EXIT INT TERM
    ;;
  verify)
    psql_owned -At < verify.sql
    psql_owned -At < inventory.sql > inventory-after.txt
    diff -u inventory-before.txt inventory-after.txt
    docker inspect "$backend" --format '{{.Config.Image}} {{.State.Status}}'
    docker inspect "$frontend" --format '{{.Config.Image}} {{.State.Status}}'
    ;;
  *) exit 2 ;;
esac

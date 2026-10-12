#!/usr/bin/env bash
set -euo pipefail
umask 077
release=$PWD
[[ "$release" == /var/lib/eduivme/releases/assessment-v2-* ]] || exit 2
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]] || exit 2
tag="assessment-v2-20261012-${revision:0:8}"
api_image="eduivme/speed-reading-service:$tag"
web_image="eduivme/speed-reading-frontend:$tag"
backend=eduivme-production-speed-reading-service-1
frontend=eduivme-production-speed-reading-frontend-1
exec 9>/var/lock/eduivme-speed-reading-data-release.lock
flock -n 9 || { echo 'Another release is running' >&2; exit 2; }

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
ready() {
 for attempt in {1..20}; do
  ip=$(docker inspect "$backend" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -n1)
  if curl -fsS --max-time 4 "http://$ip:8080/health/ready" > health-api.json &&
   [[ $(docker inspect "$frontend" --format '{{.State.Health.Status}}') == healthy ]]; then return 0; fi
  sleep 3
 done
 return 1
}
psql_owned() { docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U eduplatform -d "$1" "${@:2}"; }
backup() {
 test ! -e "$1"
 docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > "$1"
 test -s "$1"
 docker exec -i postgres pg_restore --list < "$1" > "$1.contents.txt"
 sha256sum "$1" > "$1.sha256"
}
check_images() {
 for image in "$api_image" "$web_image"; do
  [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
 done
}
case "${1:-build}" in
 build)
  test ! -e previous-api-image.txt
  sha256sum -c payload.sha256
  docker inspect "$backend" --format '{{.Image}}' > previous-api-image.txt
  docker inspect "$frontend" --format '{{.Image}}' > previous-web-image.txt
  mkdir -p previous/browser
  docker cp "$frontend:/usr/share/nginx/html/." previous/browser/
  docker cp "$frontend:/etc/nginx/conf.d/default.conf" nginx.conf
  printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$api_image" "$web_image" > production.override.yml
  printf 'services:\n  speed-reading-service:\n    image: %s\n    pull_policy: never\n  speed-reading-frontend:\n    image: %s\n    pull_policy: never\n' "$(<previous-api-image.txt)" "$(<previous-web-image.txt)" > rollback.override.yml
  prepare_compose
  docker build --build-arg "SOURCE_REVISION=$revision" -f api.Dockerfile -t "$api_image" .
  docker build --build-arg "SOURCE_REVISION=$revision" -f web.Dockerfile -t "$web_image" .
  check_images
  docker run --rm "$web_image" nginx -t
  sha256sum payload.sha256 > built-payload.sha256
  ;;
 rehearse)
  rm -f -- "$release/rehearsal-passed.txt"
  sha256sum -c built-payload.sha256
  sha256sum -c payload.sha256; check_images
  validation_db="assessment_v2_${revision:0:12}"
  [[ "$validation_db" =~ ^assessment_v2_[a-f0-9]{12}$ ]]
  backup rehearsal-before.dump
  docker exec postgres createdb -U eduplatform -T template0 "$validation_db"
  trap 'docker exec postgres dropdb -U eduplatform "$validation_db"' EXIT
  docker exec -i postgres pg_restore -U eduplatform -d "$validation_db" < rehearsal-before.dump
  psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-before.txt
  psql_owned "$validation_db" < apply.sql
  psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-after.txt
  diff -u rehearsal-before.txt rehearsal-after.txt
  psql_owned "$validation_db" -qAt -c "SELECT id,configuration_json,description,version FROM speed_reading.exercises ORDER BY id" > rehearsal-first.txt
  psql_owned "$validation_db" < apply.sql
  psql_owned "$validation_db" -qAt -c "SELECT id,configuration_json,description,version FROM speed_reading.exercises ORDER BY id" > rehearsal-second.txt
  diff -u rehearsal-first.txt rehearsal-second.txt
  psql_owned "$validation_db" < verify.sql
  psql_owned "$validation_db" < rollback.sql
  psql_owned "$validation_db" -qAt < inventory.sql > rehearsal-rollback.txt
  diff -u rehearsal-before.txt rehearsal-rollback.txt
  sha256sum payload.sha256 > rehearsal-passed.txt
  ;;
 promote)
  test -s rehearsal-passed.txt
  sha256sum -c rehearsal-passed.txt
  sha256sum -c built-payload.sha256
  sha256sum -c payload.sha256
  test ! -e deployed-at.txt
  check_images; prepare_compose
  [[ $(docker inspect "$backend" --format '{{.Image}}') == "$(<previous-api-image.txt)" ]]
  [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-web-image.txt)" ]]
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
  succeeded=false
  stopped=false
  changed=false
  recover() {
   status=$?
   trap - EXIT INT TERM
   rm -f -- "$release/runtime.env" || true
   if [[ "$succeeded" != true && "$stopped" == true ]]; then
    set +e
    echo 'Restoring previous Speed Reading configuration and images' >&2
    docker stop --time 30 "$backend" >/dev/null
    if [[ "$changed" == true ]]; then psql_owned speedreading_owned_db < rollback.sql || { echo 'Rollback failed; API left stopped for manual recovery' >&2; exit 1; }; fi
    "${compose[@]}" -f "$release/rollback.override.yml" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
   fi
   exit "$status"
  }
  trap recover EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  stopped=true
  docker stop --time 30 "$backend"
  backup speedreading-before.dump
  psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-before.txt
  changed=true
  psql_owned speedreading_owned_db < apply.sql
  psql_owned speedreading_owned_db < verify.sql
  psql_owned speedreading_owned_db -qAt < inventory.sql > inventory-after.txt
  diff -u inventory-before.txt inventory-after.txt
  "${compose[@]}" up -d --no-deps --pull never speed-reading-service speed-reading-frontend
  ready
  docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
  diff -u <(grep -Ev "^($backend|$frontend) " containers-before.txt) <(grep -Ev "^($backend|$frontend) " containers-after.txt)
  date -u +%FT%TZ > deployed-at.txt
  succeeded=true
  trap - EXIT INT TERM
  ;;
 *) exit 2 ;;
esac

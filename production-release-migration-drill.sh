#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/platform-release-20260928-2cedc8f0
service=${1:?service required}
case "$service" in
 identity-service) database=identity_db; connection_key=ConnectionStrings__DefaultConnection ;;
 coaching-service) database=coaching_db; connection_key=ConnectionStrings__DefaultConnection ;;
 notification-service) database=notification_db; connection_key=ConnectionStrings__DefaultConnection ;;
 speed-reading-service) database=speedreading_owned_db; connection_key=ConnectionStrings__SpeedReadingOwned ;;
 *) exit 2 ;;
esac
drill_database="${database}_release_2cedc8f0_drill"
container="eduivme-production-${service}-1"
if [[ "${2:-restore}" == restore ]]; then
  docker exec postgres sh -lc 'createdb -U "$POSTGRES_USER" "$1"' sh "$drill_database"
  docker exec -i postgres sh -lc 'pg_restore -U "$POSTGRES_USER" -d "$1" --no-owner' sh "$drill_database" < "$release/backups/$database.dump"
fi
network=$(docker inspect "$container" --format '{{range $name, $value := .NetworkSettings.Networks}}{{$name}}{{end}}')
connection=$(docker inspect "$container" --format '{{range .Config.Env}}{{println .}}{{end}}' | grep "^${connection_key}=" | cut -d= -f2- | sed "s/Database=$database/Database=$drill_database/")
test -n "$connection"
[[ "$connection" == *"Database=$drill_database"* ]]
if [[ "$service" == speed-reading-service ]]; then
  user=$(docker exec postgres printenv POSTGRES_USER)
  password=$(docker exec postgres printenv POSTGRES_PASSWORD)
  connection="Host=postgres;Port=5432;Database=$drill_database;Username=$user;Password=$password"
fi
docker run --rm --network "$network" \
  --env-file <(docker inspect "$container" --format '{{range .Config.Env}}{{println .}}{{end}}') \
  -e "$connection_key=$connection" \
  "eduivme/$service:platform-release-20260928-2cedc8f0" --migrate-only
printf 'Migration drill passed: %s\n' "$service"

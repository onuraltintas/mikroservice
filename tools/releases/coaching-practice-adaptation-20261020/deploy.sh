set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-practice-adaptation-20261020
tag=coaching-practice-adaptation-20261020
drill=coaching_practice_drill_20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
payload(){ sha256sum -c payload.sha256 > payload-verified.log; }
baseline(){ for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done; }
stable_others(){
  while read -r name id; do
    case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
  done < containers.before.txt
}
ready(){
  local service=$1 ip port=8080 path=/health/ready
  [[ "$service" == admin-panel ]] && { port=4000; path=/; }
  [[ "$service" == staff-portal ]] && { port=80; path=/staff/; }
  for attempt in $(seq 1 30); do
    ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  return 1
}
protected(){
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5((to_jsonb(t)-%L-%L-%L-%L-%L)::text),%L ORDER BY md5((to_jsonb(t)-%L-%L-%L-%L-%L)::text)),%L)) FROM coaching.%I t;',
 table_name,'CreatedByTeacherId','topic_analysis','practice_exam','target_net','target_practice_exam_code','','CreatedByTeacherId','topic_analysis','practice_exam','target_net','target_practice_exam_code','',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name \gexec
COMMIT;
SQL
}
migrate(){
  local database=$1 file="$release/migrate.env"
  [[ "$database" == coaching_db || "$database" == "$drill" ]]
  local connection
  connection=$(jq -er '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
  [[ "$connection" == *'Database=coaching_db;'* ]]
  if [[ "$database" != coaching_db ]]; then connection=$(jq -nr --arg c "$connection" --arg db "$database" '$c|sub("Database=coaching_db;";"Database="+$db+";")'); fi
  [[ "$connection" == *"Database=$database;"* ]]
  printf 'ConnectionStrings__DefaultConnection=%s\n' "$connection" > "$file"
  # Only the DB connection is used by --migrate-only; no bus, host or seed runs.
  docker run --rm --network "$(<network.txt)" --entrypoint dotnet --env-file "$file" -e ASPNETCORE_ENVIRONMENT=Production "eduivme/coaching-service:$tag" Coaching.API.dll --migrate-only > "$database-migrate.log" 2>&1
  rm -f "$file"
}
case "${1:-}" in
prepare)
  test ! -e prepared.txt; payload
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 1') == '20261020075034_UnifiedTargetCatalogConstraints' ]]
  # This release has a legacy-result cleanup migration. The live baseline contains none.
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c "SELECT count(*) FROM coaching.exam_results r JOIN coaching.exams e ON e.id=r.exam_id WHERE e.exam_type IN ('LGS','YKS')") == 0 ]]
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c "SELECT count(*) FROM coaching.exams WHERE exam_type IN ('LGS','YKS')") == 0 ]]
  docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1 > network.txt
  cp /var/lib/eduivme/releases/unified-target-catalog-20261020/production.compose.json base.compose.json
  docker inspect $(docker ps -q) | jq '{services:(map(select(.Config.Labels["com.docker.compose.project"]=="eduivme-production" and .Config.Labels["com.docker.compose.service"]!=null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
  jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
  for service in "${services[@]}"; do
    docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
    docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
    docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
    jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.compose.json; mv next.compose.json frozen.compose.json
  done
  jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
  jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; done
  sha256sum *.before.backup > backups.sha256
  protected coaching_db > protected.before.txt
  date -u +%FT%TZ > prepared.txt
  echo 'Baselines and all three database backups are ready.'
  ;;
build)
  test -s prepared.txt; test ! -e built.txt; payload; baseline
  if [[ ! -d source/admin-panel/node_modules ]]; then cp -a /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/node_modules source/admin-panel/node_modules; fi
  cp build-portals.cjs source/admin-panel/build-portals.cjs
  docker run --rm --name coaching-practice-portal-builder-20261020 --memory=4g --cpus=2 --network none \
    -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > portals-build.log 2>&1
  mkdir -p payload/admin payload/staff
  cp -a source/admin-panel/dist/admin-panel/. payload/admin/
  cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
  test -s payload/admin/browser/index.csr.html; grep -q 'base href="/staff/"' payload/staff/index.html
  # Preserve the original source manifest; seal generated runtime files separately.
  find payload -type f | LC_ALL=C sort | xargs sha256sum > runtime.sha256
  for service in "${services[@]}"; do
    docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
    docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
    docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
  done
  stable_others; date -u +%FT%TZ > built.txt
  echo 'Exact-baseline API and Linux-built portal images are ready.'
  ;;
drill)
  test -s built.txt; test ! -e drill-verified.txt; baseline
  connection=$(jq -er '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
  pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
  docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
  docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
  protected "$drill" > drill-protected.before.txt
  migrate "$drill"
  protected "$drill" > drill-protected.after.txt; cmp drill-protected.before.txt drill-protected.after.txt
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d "$drill" -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 1') == '20261020100033_PracticeTopicAdaptation' ]]
  docker exec postgres dropdb -U "$db_user" "$drill"
  stable_others; date -u +%FT%TZ > drill-verified.txt
  echo 'All pending migrations passed on the restored copy; protected rows match.'
  ;;
promote)
  test -s drill-verified.txt; test ! -e deployed.txt; baseline; stable_others
  sha256sum -c runtime.sha256 > runtime-check.log
  recover(){
    local status=$?; trap - EXIT
    if (( status != 0 )); then
      for service in "${services[@]}"; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" >> recovery.log 2>&1 || true; done
      echo 'Previous images restored; additive schema and all new writes retained. Inspect recovery.log.' >&2
    fi
    exit "$status"
  }
  trap recover EXIT
  "${compose[@]}" -f rollback.compose.json stop coaching-service > stop.log 2>&1
  docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-promotion.backup
  sha256sum coaching_db.before-promotion.backup > promotion-backup.sha256
  protected coaching_db > promotion-protected.before.txt
  migrate coaching_db
  protected coaching_db > promotion-protected.after.txt; cmp promotion-protected.before.txt promotion-protected.after.txt
  for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1; ready "$service"; done
  stable_others; date -u +%FT%TZ > deployed.txt; trap - EXIT
  echo 'Three services deployed; every protected pre-existing row is unchanged.'
  ;;
verify)
  test -s deployed.txt
  for service in "${services[@]}"; do ready "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; done
  stable_others
  docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 3'
  echo 'Readiness, exact image identities and unrelated service preservation verified.'
  ;;
*) echo 'Expected prepare, build, drill, promote or verify.' >&2; exit 2;;
esac

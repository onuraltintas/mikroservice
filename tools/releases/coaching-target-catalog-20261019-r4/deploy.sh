set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-target-catalog-20261019-r4
drill=coaching_yks_catalog_drill_20261019_r4
tag=coaching-target-catalog-20261019-r4
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
payload(){ sha256sum -c payload.sha256 > payload-verified.log; }

baseline_targets(){
  for service in "${services[@]}"; do
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
  done
}
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
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM coaching.%I t HAVING count(*) > 0;',table_name,'','',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('target_university_programs','target_admission_records','target_admission_batches','academic_goals','InboxState','OutboxState','OutboxMessage','AdminAuditRecords','__ef_migrations_history','__EFMigrationsHistory')
ORDER BY table_name \gexec
COMMIT;
SQL
}
batch_rows(){
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
SELECT to_jsonb(b)::text FROM coaching.target_admission_batches b;
SQL
}
verify_catalog(){ docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < verify.sql > "$1-catalog-verification.log"; }
run_import(){
  local database=$1 connection image
  connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json)
  if [[ "$database" != coaching_db ]]; then
    connection=$(jq -nr --arg c "$connection" --arg d "$database" '$c|sub("Database=coaching_db;";"Database="+$d+";")')
    [[ "$connection" == *"Database=$database;"* ]]
  fi
  image=$(<built-coaching-service-image.txt)
  docker run --rm --network "$(<network.txt)" --entrypoint dotnet \
    -e "TARGET_ADMISSIONS_CONNECTION=$connection" \
    -v "$release/operator:/operator:ro" -v "$release/catalog:/catalog:ro" \
    "$image" /operator/TargetAdmissions.dll --database "$database" --apply \
    --program-catalog /catalog/yks-results.json --package /catalog/ydt-all-languages.json \
    > "$database-import.log"
}

case "${1:-}" in
prepare)
  test ! -e prepared.txt
  payload
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Config.Image}}') == 'eduivme/coaching-service:coaching-targets-20261019' ]]
  [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Config.Image}}') == 'eduivme/admin-panel:coaching-targets-20261019-r3' ]]
  [[ $(docker inspect eduivme-production-staff-portal-1 --format '{{.Config.Image}}') == 'eduivme/staff-portal:coaching-targets-20261019' ]]
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT * FROM coaching.__ef_migrations_history ORDER BY 1 DESC LIMIT 1' | cut -d'|' -f1) == '20261019095937_TargetAdmissions2026' ]]
  docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1 > network.txt
  "${compose[@]}" -f /var/lib/eduivme/releases/coaching-clean-start-20261018/production.compose.json config --format json > base.compose.json
  docker inspect $(docker ps -q) | jq '{services:(map(select(.Config.Labels["com.docker.compose.project"]=="eduivme-production" and .Config.Labels["com.docker.compose.service"]!=null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
  jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
  for service in "${services[@]}"; do
    docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
    docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
    docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
    jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.compose.json
    mv next.compose.json frozen.compose.json
  done
  jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
  jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; done
  sha256sum *.before.backup > backups.sha256
  protected coaching_db > protected.before.txt
  batch_rows coaching_db > batches.before.txt
  date -u +%FT%TZ > prepared.txt
  echo 'Exact image baselines, live database backups and protected data fingerprints prepared.'
  ;;
build)
  test -s prepared.txt; test ! -e built.txt; payload; baseline_targets
  for service in "${services[@]}"; do
    docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
    docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
    docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
  done
  stable_others; date -u +%FT%TZ > built.txt
  echo 'API and both portal images built from the exact live baselines.'
  ;;
drill)
  test -s built.txt; test ! -e drill-verified.txt; baseline_targets
  connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json)
  pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $pattern ]]; owner=${BASH_REMATCH[2]}
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
  docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
  docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before.backup > drill-restore.log 2>&1
  protected "$drill" > drill-protected.before.txt
  batch_rows "$drill" > drill-batches.before.txt
  jq --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|=sub("Database=coaching_db;";"Database="+$d+";")' production.compose.json > drill.compose.json
  jq -e --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$d+";")' drill.compose.json >/dev/null
  "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migrate.log 2>&1
  run_import "$drill"
  verify_catalog "$drill"
  protected "$drill" > drill-protected.after.txt; cmp drill-protected.before.txt drill-protected.after.txt
  batch_rows "$drill" > drill-batches.after.txt
  comm -23 drill-batches.before.txt drill-batches.after.txt > drill-missing-old-batches.txt
  test ! -s drill-missing-old-batches.txt
  stable_others
  docker exec postgres dropdb -U "$db_user" "$drill"
  date -u +%FT%TZ > drill-verified.txt
  cat "$drill-catalog-verification.log"
  echo 'Rehearsal passed; pre-existing plans, goals, schools, work rows and annual batches are preserved.'
  ;;
promote)
  test -s drill-verified.txt; test ! -e deployed.txt; payload; baseline_targets; stable_others
  for database in coaching_db identity_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup"; done
  sha256sum *.before-promotion.backup > promotion-backups.sha256
  protected coaching_db > promotion-protected.before.txt
  batch_rows coaching_db > promotion-batches.before.txt
  recover(){
    local status=$?; trap - EXIT
    if (( status != 0 )); then
      if [[ -e promotion-schema-applied.txt ]]; then
        docker exec -i postgres pg_restore --clean --if-exists --exit-on-error --no-owner --no-privileges -U "$db_user" -d coaching_db < coaching_db.before-promotion.backup > restore-coaching-db.log 2>&1 || true
      fi
      "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never coaching-service > recovery.log 2>&1 || true
      ready coaching-service || true
      for service in admin-panel staff-portal; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" >> recovery.log 2>&1 || true; done
    fi
    exit "$status"
  }
  trap recover EXIT
  "${compose[@]}" -f rollback.compose.json stop coaching-service > stop.log 2>&1
  "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migrate.log 2>&1
  date -u +%FT%TZ > promotion-schema-applied.txt
  run_import coaching_db
  verify_catalog coaching_db
  for service in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1; ready "$service"; done
  protected coaching_db > promotion-protected.after.txt; cmp promotion-protected.before.txt promotion-protected.after.txt
  batch_rows coaching_db > promotion-batches.after.txt
  comm -23 promotion-batches.before.txt promotion-batches.after.txt > promotion-missing-old-batches.txt
  test ! -s promotion-missing-old-batches.txt
  stable_others
  date -u +%FT%TZ > deployed.txt
  trap - EXIT
  cat coaching_db-catalog-verification.log
  echo 'Live catalog replacement completed; all existing YKS/LGS records and protected coaching data are preserved.'
  ;;
verify)
  test -s deployed.txt; payload
  for service in "${services[@]}"; do ready "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; done
  verify_catalog coaching_db
  stable_others
  cat coaching_db-catalog-verification.log
  echo 'All three new images are healthy and the 2026 catalog totals are verified.'
  ;;
*) echo 'Expected prepare, build, drill, promote, or verify.' >&2; exit 2;;
esac
